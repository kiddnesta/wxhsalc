using System;
using System.Drawing;
using System.Windows.Forms;

namespace ClashXW
{
    /// <summary>
    /// Operation feedback that does not depend on Windows delivering tray balloons.
    /// </summary>
    internal sealed class StatusToastForm : Form
    {
        private const int HorizontalPadding = 18;
        private const int WindowMargin = 16;
        private readonly System.Windows.Forms.Timer _dismissTimer;

        public StatusToastForm(string title, string message, ToolTipIcon icon)
        {
            var isError = icon == ToolTipIcon.Error;
            var background = isError ? Color.FromArgb(83, 30, 30) : Color.FromArgb(31, 42, 52);
            var accent = isError ? Color.FromArgb(255, 123, 111) : Color.FromArgb(106, 205, 153);

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = background;
            ForeColor = Color.White;
            Font = SystemFonts.MessageBoxFont;
            AutoScaleMode = AutoScaleMode.Dpi;
            AccessibleName = $"ClashXW {title}: {message}";

            var workingArea = Screen.FromPoint(Cursor.Position).WorkingArea;
            var width = Math.Min(400, workingArea.Width - WindowMargin * 2);
            var textWidth = Math.Max(120, width - HorizontalPadding * 2 - 32);
            var messageSize = TextRenderer.MeasureText(message, Font,
                new Size(textWidth, 180), TextFormatFlags.WordBreak);
            var height = Math.Max(90, Math.Min(240, messageSize.Height + 58));
            Size = new Size(width, height);
            Location = new Point(workingArea.Right - width - WindowMargin,
                workingArea.Bottom - height - WindowMargin);

            var accentBar = new Panel
            {
                BackColor = accent,
                Bounds = new Rectangle(0, 0, 4, height)
            };
            Controls.Add(accentBar);

            var titleLabel = new Label
            {
                AutoSize = false,
                Bounds = new Rectangle(HorizontalPadding, 12, textWidth, 23),
                Font = new Font(Font, FontStyle.Bold),
                ForeColor = Color.White,
                Text = title,
                AccessibleName = title
            };
            Controls.Add(titleLabel);

            var messageLabel = new Label
            {
                AutoSize = false,
                Bounds = new Rectangle(HorizontalPadding, 38, width - HorizontalPadding * 2, height - 48),
                ForeColor = Color.White,
                Text = message,
                AccessibleName = message
            };
            Controls.Add(messageLabel);

            var closeButton = new Button
            {
                FlatStyle = FlatStyle.Flat,
                Bounds = new Rectangle(width - 38, 9, 28, 28),
                Text = "×",
                ForeColor = Color.White,
                BackColor = background,
                TabStop = false,
                AccessibleName = "Dismiss notification"
            };
            closeButton.FlatAppearance.BorderSize = 0;
            closeButton.Click += (_, _) => Close();
            Controls.Add(closeButton);

            _dismissTimer = new System.Windows.Forms.Timer { Interval = isError ? 12000 : 6500 };
            _dismissTimer.Tick += (_, _) => Close();
            Shown += (_, _) => _dismissTimer.Start();
        }

        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                var parameters = base.CreateParams;
                parameters.ExStyle |= 0x00000080 | 0x08000000; // WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE
                return parameters;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _dismissTimer.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
