using System;
using System.ComponentModel;
using System.Windows.Forms;

namespace LCS.Theming
{
    // Base of all windows: theme colors and matching title bar.
    public class ThemedForm : Form
    {
        private static bool IsDesigner => LicenseManager.UsageMode == LicenseUsageMode.Designtime;

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (!IsDesigner && !DesignMode)
            {
                NativeTheme.ApplyWindowChrome(this, Theme.Current);
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            if (!IsDesigner && !DesignMode)
            {
                ThemeManager.Apply(this);
            }
            base.OnLoad(e);
        }
    }
}
