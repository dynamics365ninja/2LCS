using System;
using System.Windows.Forms;

namespace LCS.Forms
{
    public partial class CookieEdit : LCS.Theming.ThemedForm
    {
        private const int CpNocloseButton = 0x200;

        public CookieEdit()
        {
            InitializeComponent();
        }

        public bool Cancelled { get; private set; }
        public string Cookie { get; private set; }

        protected override CreateParams CreateParams
        {
            get
            {
                var myCp = base.CreateParams;
                myCp.ClassStyle |= CpNocloseButton;
                return myCp;
            }
        }

        private void CancelButton_Click(object sender, EventArgs e)
        {
            Cancelled = true;
            Close();
        }

        private void CookieEdit_Load(object sender, EventArgs e)
        {
            cookieTextBox.Text = Auth.LcsSession.StoredCookie;
        }

        private void OkButton_Click(object sender, EventArgs e)
        {
            Cookie = cookieTextBox.Text;
            Close();
        }
    }
}