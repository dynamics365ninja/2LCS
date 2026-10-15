using LCS.Theming;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LCS.Forms
{
    public partial class AddNsg : LCS.Theming.ThemedForm
    {
        private const int CpNocloseButton = 0x200;

        // HTTPS - the address ends up in a firewall rule.
        private static readonly HttpClient IpLookupClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        private const string IpLookupUrl = "https://ipv4.icanhazip.com";

        public AddNsg()
        {
            InitializeComponent();
        }

        protected override async void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            await PopulateWithOwnIPAsync();
        }

        public bool Cancelled { get; private set; }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Dictionary<string, string> Rule { get; set; }

        protected override CreateParams CreateParams
        {
            get
            {
                var myCp = base.CreateParams;
                myCp.ClassStyle = myCp.ClassStyle | CpNocloseButton;
                return myCp;
            }
        }

        private void Button1_Click(object sender, EventArgs e)
        {
            if (!ValidateRule())
            {
                Cancelled = true;
            }
            Close();
        }

        private void CancelButton_Click(object sender, EventArgs e)
        {
            Cancelled = true;
            Close();
        }

        private async Task PopulateWithOwnIPAsync()
        {
            if (!Properties.Settings.Default.populateOwnIPForNSG) return;
            try
            {
                var response = (await IpLookupClient.GetStringAsync(IpLookupUrl)).Trim();
                // Only a plain IPv4 address goes into the rule.
                if (IPAddress.TryParse(response, out var address) && address.AddressFamily == AddressFamily.InterNetwork)
                {
                    if (string.IsNullOrEmpty(textBox2.Text)) textBox2.Text = address.ToString();
                    return;
                }
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
            {
                // Reported below.
            }
            if (!IsDisposed && string.IsNullOrEmpty(textBox2.Text))
            {
                FluentMessageBox.Show(this, "Failed to get your IP address. Please enter it manually.");
            }
        }

        private bool ValidateRule()
        {
            if (string.IsNullOrEmpty(textBox1.Text))
            {
                FluentMessageBox.Show(this, "Rule name is empty.");
                return false;
            }
            if (string.IsNullOrEmpty(textBox2.Text))
            {
                FluentMessageBox.Show(this, "IP address field is empty.");
                return false;
            }
            Rule = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { textBox1.Text, textBox2.Text}
            };
            return true;
        }
    }
}