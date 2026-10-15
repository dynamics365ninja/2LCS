# 2LCS - Lifecycle Services companion app

This small utility can help you manage D365FO instances deployed in your LCS project. Both cloud-hosted - in your Azure subscription and Microsoft-hosted sandboxes. It offers only a subset of functionalities that LCS offers, but you can execute them a bit faster.
To download latest version go to [Releases section](https://github.com/Microsoft/2LCS/releases) and download zip archive (2LCS-X.X.X.X.zip) with compiled app. 
# Quick tips

* After you run it for the first time you need to sign in to LCS in the app (**Sign in**). Sign-in uses the embedded Microsoft Edge (WebView2) browser; on Microsoft Entra joined or registered devices you are usually signed in with your Windows account automatically.
* Second step is to choose one of the projects you can access in LCS (the project button at the top of the window, or **Change project**).
* Third one is to refresh the list of D365 instances for chosen project (**Refresh** or F5).

Select an environment to see its details (identifiers with copy buttons, versions, links) in the pane on the right, and the most common actions (**Log on**, **Connect**, **Start**, **Stop**) in the command bar. All actions are available by right-clicking an environment or with the **...** button. Most commands work on several environments at once - select them with Ctrl or Shift. Use the filter box (Ctrl+F) to quickly find an environment. Right-click a column header to choose the visible columns; click a header to sort. 2LCS remembers the columns, sorting, the window position, the last tab and the filter.

Long running operations do not block the window; their result is shown in a bar above the list (and as a Windows notification when 2LCS is in the background).

# Staying signed in

LCS sessions expire after about an hour. 2LCS renews the session automatically in the background, so you do not have to sign in again every hour:

* the session is renewed silently before it expires (every 40 minutes) and after the computer wakes up from sleep,
* a request that hits an expired session is renewed and retried transparently.

The renewal uses a hidden embedded browser with a persistent profile (`%LOCALAPPDATA%\2LCS\WebView2`), so it works as long as your Microsoft Entra ID sign-in is valid. If Entra ID requires an interaction (for example multi-factor authentication or a password change), the session indicator in the top right corner shows **Sign-in required** - click it to sign in again. The automatic renewal can be turned off in **Settings** (*Keep me signed in*). **Sign out** also clears the embedded browser profile.

The app requires the [Microsoft Edge WebView2 Runtime](https://go.microsoft.com/fwlink/p/?LinkId=2124703), which is part of Windows 11 and current Windows 10 installations.

# Real state of cloud-hosted VMs

LCS only knows about actions done through LCS: a VM shut down from inside Windows, in the Azure portal or by an auto-shutdown policy still shows *Deployed* in LCS. 2LCS therefore also reads the power state of cloud-hosted VMs directly from Azure (column **VM State (Azure)**: *Running* or *Stopped*; the exact Azure state is in the tooltip and the details pane). When it differs from LCS, the row shows a warning and the details pane explains it (for example a VM that is stopped but not deallocated is still billed).

Reading the state requires read access (Reader role) to the Azure subscription / resource group of the environment. 2LCS signs in to Azure with your Windows account through the Windows account broker; when that is not possible, it offers a **Sign in to Azure** button instead of prompting on its own. The feature can be turned off in **Settings**.

The VM state is checked every 15 minutes while the VMs are running or stopped (*Settings > Check the VM state*: 5, 15, 30, 60 minutes or only with Refresh) and every minute while a VM is starting or stopping. After Start/Stop from 2LCS, the environment is checked in LCS and Azure 30 seconds, 2, 5 and 10 minutes later, until it reaches its new state. LCS is not polled otherwise - use Refresh (F5) for changes made outside 2LCS.

# Appearance

2LCS follows the Windows light/dark app mode and accent color. The theme can be changed in **Settings**; the change is applied immediately.

# Data stored on your computer

The LCS session cookie and the optional credentials cache (*Settings > Credentials cache*) are stored encrypted with Windows DPAPI, so only your Windows account on this computer can read them. Credentials cached in plain text by older versions are migrated and the old file is deleted.

# No support

**Please note that this is only a sample code that is not supported by Microsoft in anyway.** It is provided AS IS and it can break anytime because of changes introduced in LCS. 

# Contributing

This project welcomes contributions and suggestions.  Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit https://cla.microsoft.com.

When you submit a pull request, a CLA-bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., label, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.
