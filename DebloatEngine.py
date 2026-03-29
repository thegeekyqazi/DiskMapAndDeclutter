import subprocess
import json
import winreg

# ⚡ THE ULTIMATE BLOATWARE HITLIST
BLOATWARE_HITLIST = {
    # --- Xbox & Gaming Bloat ---
    "Microsoft.XboxApp": "Xbox App",
    "Microsoft.GamingApp": "Xbox Gaming App",
    "Microsoft.XboxGamingOverlay": "Xbox Game Bar",
    "Microsoft.XboxSpeechToTextOverlay": "Xbox Speech to Text",
    "Microsoft.XboxIdentityProvider": "Xbox Identity Provider",
    "Microsoft.MicrosoftSolitaireCollection": "Microsoft Solitaire Collection",
    
    # --- Microsoft "Fluff" Apps ---
    "Microsoft.BingWeather": "MSN Weather",
    "Microsoft.BingNews": "MSN News",
    "Microsoft.BingSports": "MSN Sports",
    "Microsoft.BingFinance": "MSN Money",
    "Microsoft.ZuneVideo": "Movies & TV",
    "Microsoft.ZuneMusic": "Windows Media Player / Groove",
    "microsoft.windowscommunicationsapps": "Mail & Calendar",
    "Microsoft.WindowsMaps": "Windows Maps",
    "Microsoft.GetHelp": "Get Help",
    "Microsoft.Getstarted": "Windows Tips",
    "Microsoft.MixedReality.Portal": "Mixed Reality Portal",
    "Microsoft.YourPhone": "Phone Link",
    "Microsoft.MicrosoftOfficeHub": "Office Hub",
    "Microsoft.WindowsFeedbackHub": "Feedback Hub",
    "Microsoft.People": "Microsoft People",
    "Microsoft.SkypeApp": "Skype",
    "Microsoft.Todos": "Microsoft To Do",
    "Microsoft.PowerAutomateDesktop": "Power Automate",
    
    # --- 3D & Creator Bloat ---
    "Microsoft.Microsoft3DViewer": "3D Viewer",
    "Microsoft.3DBuilder": "3D Builder",
    "Microsoft.Paint3D": "Paint 3D",
    "Microsoft.Print3D": "Print 3D",
    
    # --- System Utilities (Usually safe to remove if unused) ---
    "Microsoft.WindowsSoundRecorder": "Voice Recorder",
    "Microsoft.WindowsCamera": "Windows Camera",
    "Microsoft.WindowsAlarms": "Alarms & Clock",
    "Microsoft.549981C3F5F10": "Cortana",
    
    # --- Third-Party OEM Junk ---
    "king.com.CandyCrushSaga": "Candy Crush Saga",
    "king.com.CandyCrushSodaSaga": "Candy Crush Soda",
    "SpotifyAB.SpotifyMusic": "Spotify",
    "Disney": "Disney+",
    "Netflix": "Netflix",
    "HuluLLC.HuluPlus": "Hulu",
    "Facebook": "Facebook",
    "Instagram": "Instagram",
    "Twitter": "Twitter / X",
    "LinkedInforWindows": "LinkedIn",
    "ByteDanceVideo.TikTok": "TikTok",
    "McAfee": "McAfee Antivirus",
}

def scan_bloatware() -> list:
    """Uses PowerShell to list installed apps, then filters for known bloatware."""
    found_bloatware = []
    ps_command = 'Get-AppxPackage | Select-Object Name, PackageFullName | ConvertTo-Json'
    
    try:
        result = subprocess.run(
            ["powershell", "-Command", ps_command], 
            capture_output=True, text=True, creationflags=subprocess.CREATE_NO_WINDOW
        )
        if result.returncode == 0 and result.stdout.strip():
            installed_apps = json.loads(result.stdout)
            if isinstance(installed_apps, dict):
                installed_apps = [installed_apps]
            
            # ⚡ Upgraded Matching Logic: Makes matching case-insensitive for safety
            lower_hitlist = {k.lower(): v for k, v in BLOATWARE_HITLIST.items()}
                
            for app in installed_apps:
                name = app.get("Name", "")
                if name and name.lower() in lower_hitlist:
                    found_bloatware.append({
                        "id": name,
                        "display_name": lower_hitlist[name.lower()],
                        "package_full_name": app.get("PackageFullName", "")
                    })
    except Exception as e:
        print(f"Scan error: {e}")
    return found_bloatware

def remove_bloatware(package_full_names: list) -> dict:
    """Silently executes the PowerShell removal command for selected apps."""
    results = {"success": [], "failed": []}
    for package in package_full_names:
        ps_command = f'Remove-AppxPackage -Package "{package}"'
        try:
            result = subprocess.run(
                ["powershell", "-Command", ps_command], 
                capture_output=True, text=True, creationflags=subprocess.CREATE_NO_WINDOW
            )
            if result.returncode == 0:
                results["success"].append(package)
            else:
                results["failed"].append({"package": package, "error": result.stderr.strip()})
        except Exception as e:
            results["failed"].append({"package": package, "error": str(e)})
    return results

def disable_windows_telemetry() -> dict:
    """
    WinUtil's famous Privacy tweak. 
    Edits the registry to disable Microsoft Data Collection and Telemetry.
    """
    paths_to_tweak = [
        (winreg.HKEY_LOCAL_MACHINE, r"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry", 0),
        (winreg.HKEY_LOCAL_MACHINE, r"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "MaxTelemetryAllowed", 0),
        (winreg.HKEY_CURRENT_USER, r"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "Start_TrackProgs", 0)
    ]
    
    success_count = 0
    for hkey, path, key_name, value in paths_to_tweak:
        try:
            # Create key if it doesn't exist, then set the value
            winreg.CreateKey(hkey, path)
            with winreg.OpenKey(hkey, path, 0, winreg.KEY_WRITE) as key:
                winreg.SetValueEx(key, key_name, 0, winreg.REG_DWORD, value)
            success_count += 1
        except PermissionError:
            return {"status": "error", "message": "Administrator privileges required to alter Telemetry."}
        except Exception as e:
            pass

    return {"status": "success", "message": f"Applied {success_count} privacy registry tweaks."}