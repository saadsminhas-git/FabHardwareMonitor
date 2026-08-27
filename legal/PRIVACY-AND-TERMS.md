FAB HARDWARE MONITOR
Privacy Policy and Terms of Use
Effective 17 August 2026  ·  Version 1

Please read this before installing Fab Hardware Monitor. By ticking “I agree” in the installer, you accept this document.

WHO WE ARE
Fab Hardware Monitor is published by Fabric Visuals Ltd (“we”, “us”). Website: https://www.fabricvisuals.com

WHAT THE APP DOES
The app is a Windows taskbar widget. It reads hardware and system counters on this PC (network throughput, CPU and memory use, GPU use and memory, and temperatures where available) and shows them on the taskbar.

PRIVACY — WHAT STAYS ON THIS PC
Hardware readings are processed in memory on this computer so they can be displayed. They are not uploaded to Fabric Visuals.
Settings (refresh interval, which devices to show, appearance, autostart, update preference) are stored locally in:
  %AppData%\FabHardwareMonitor\settings.json
If the app crashes, a technical log may be written to:
  %AppData%\FabHardwareMonitor\error.log
That log can include exception text from this PC. It is not sent to us automatically.

We do not create an account. We do not ask for your name, email, or payment details. We do not sell, rent, or share your data. We do not use advertising or analytics SDKs.

PRIVACY — NETWORK CONTACT
If updates are enabled (they are on by default; you can turn them off in Settings), the app contacts GitHub to see whether a newer version exists and to download it. GitHub will see a normal HTTPS request, which typically includes your IP address and a user-agent. We do not receive that request. GitHub’s own privacy policy applies to that traffic.

OPTIONAL KERNEL DRIVER
CPU temperature on many PCs needs PawnIO, a third-party kernel driver. Installing it is optional (this wizard or Settings later). Other metrics keep working if you skip it. Kernel drivers run with high privilege. If you install PawnIO, that installer’s terms apply to the driver.

AUTOSTART
The app can register a Windows Task Scheduler sign-in task so it starts when you sign in. If Task Scheduler is blocked, it falls back to a per-user Run key. That is on by default. You can turn it off in Settings, or uninstall the app.

YOUR CHOICES
You can refuse this document and not use the app. You can uninstall at any time. Uninstall removes the app, shortcuts, the logon task, local settings, and crash logs. It does not remove PawnIO if you installed that driver.

CONTACT
Questions about this document: https://www.fabricvisuals.com

TERMS OF USE
1. Licence. We grant you a non-exclusive licence to install and use Fab Hardware Monitor on Windows PCs you own or are authorised to use.
2. No warranty. The app is provided “as is”. We do not warrant that readings are accurate, that the app will be uninterrupted or error-free, or that it is fit for a particular purpose.
3. Not a medical or safety device. The heartbeat graphic is branding only. Do not rely on this app for health, medical, industrial safety, or any purpose where a wrong reading could cause harm.
4. Hardware access. Reading sensors and (if you install it) running a kernel driver can affect system stability. You use the app at your own risk.
5. Liability. To the fullest extent permitted by English law, Fabric Visuals Ltd is not liable for any loss of data, hardware damage, downtime, or other loss arising from the app. Nothing in this document limits liability for death or personal injury caused by negligence, or for fraud.
6. Law. English law governs this document. The courts of England and Wales have exclusive jurisdiction, except that you may bring a claim in your country of residence if the law requires it.

Uninstalling the app ends the licence for that copy. Sections 2–6 survive uninstall.
