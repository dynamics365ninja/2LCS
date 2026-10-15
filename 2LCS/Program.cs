using LCS.Auth;
using LCS.Forms;
using LCS.Theming;
using System;
using System.Threading;
using System.Windows.Forms;

namespace LCS
{
    internal static class Program
    {
        static MainForm mainForm;

        private static void Application_ThreadException(object sender, ThreadExceptionEventArgs e)
        {
            if (!LcsSessionExpiredException.IsCauseOf(e.Exception))
            {
                // Not a session problem - report it without forcing a new sign-in.
                var error = e.Exception.GetBaseException();
                FluentMessageBox.Show(
                    $"The operation failed: {error.Message}{Environment.NewLine}{Environment.NewLine}You are still signed in to LCS. Try again later; if the problem persists, press Ctrl+C to copy these details.{Environment.NewLine}{Environment.NewLine}{error.GetType().FullName}{Environment.NewLine}{error.StackTrace}",
                    "Something went wrong", MessageBoxButtons.OK, MessageBoxIcon.Error);
                foreach (Form form in Application.OpenForms)
                {
                    form.Cursor = Cursors.Default;
                    form.UseWaitCursor = false;
                }
                return;
            }

            FluentMessageBox.Show(
                $"Your LCS session has expired and could not be renewed automatically (for example because Microsoft Entra ID asks for multi-factor authentication). Please sign in again.",
                "Sign-in required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            GetMainFormAndCloseOthers();
            if (mainForm == null) return;
            mainForm.Cursor = Cursors.Default;
            mainForm.SetLoginButtonEnabled();
            mainForm.LoginToLCSMenuItem_Click(null, null);
        }

        private static void GetMainFormAndCloseOthers()
        {
            foreach (Form form in Application.OpenForms)
            {
                if (form is MainForm locMainForm)
                {
                    mainForm = locMainForm;
                }
                else
                {
                    form.Hide();
                    form.Close();
                }
            }
        }

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        private static void Main()
        {
            Application.ThreadException += new ThreadExceptionEventHandler(Application_ThreadException);

            // Copy user settings from previous application version if necessary
            if (Properties.Settings.Default.update)
            {
                Properties.Settings.Default.Upgrade();
                Properties.Settings.Default.update = false;
                Properties.Settings.Default.Save();
            }
            // Crisp rendering on scaled displays instead of bitmap stretching.
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            ThemeManager.InitializeApplication();
            Application.ApplicationExit += (s, e) => LcsSession.Current.Shutdown();

            if (URIHandler.DetectURILaunch(Environment.GetCommandLineArgs()))
            {
                Application.Run(new RDPConnect());
            }  else
            {
                Application.Run(new MainForm());
            }
        }
    }
}
