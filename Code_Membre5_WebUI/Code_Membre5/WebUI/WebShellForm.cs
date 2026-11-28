using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Pressing.Membre5.Demo.Application;
using Pressing.Membre5.Demo.Infrastructure;

namespace Pressing.Membre5.Demo.WebUI;

// Fenêtre unique qui affiche l'interface HTML/CSS/JS (dossier WebUI/www) et la relie à Member5Service.
internal sealed class WebShellForm : Form
{
    private readonly WebView2 _web = new() { Dock = DockStyle.Fill };
    private readonly WebBridge _bridge;

    public WebShellForm(Member5Service service, PostgresCommandeRepository commandes, OptionsPressing options)
    {
        _bridge = new WebBridge(service, commandes, options);
        Text = "PressiGest | Espace de gestion";
        Width = 1360; Height = 860; MinimumSize = new Size(1000, 640);
        StartPosition = FormStartPosition.CenterScreen;
        Controls.Add(_web);
        Load += async (_, _) => await InitialiserAsync();
    }

    private async Task InitialiserAsync()
    {
        try
        {
            var data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PressiGest", "WebView2");
            var env = await CoreWebView2Environment.CreateAsync(null, data);
            await _web.EnsureCoreWebView2Async(env);
            var core = _web.CoreWebView2;
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            core.SetVirtualHostNameToFolderMapping("pressigest.local", Path.Combine(AppContext.BaseDirectory, "WebUI", "www"), CoreWebView2HostResourceAccessKind.Allow);
            core.WebMessageReceived += async (_, e) =>
            {
                // Les appels base de données (synchrones) tournent hors du thread d'affichage pour ne pas figer la fenêtre.
                var message = e.TryGetWebMessageAsString();
                var reply = await Task.Run(() => _bridge.HandleAsync(message));
                core.PostWebMessageAsString(reply);
            };
            core.Navigate("https://pressigest.local/index.html");
        }
        catch (Exception ex)
        {
            MessageBox.Show("Impossible de démarrer l'interface web (WebView2 Runtime requis).\n\n" + ex.Message, "PressiGest", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
