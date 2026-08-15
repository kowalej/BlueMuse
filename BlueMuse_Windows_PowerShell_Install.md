# BlueMuse Windows Installation: PowerShell and Developer Mode Notes

## Purpose

This file documents the common Windows issues encountered when installing BlueMuse, why they occur, and the exact steps required to resolve them. It is intended as a single reference file suitable for inclusion in a repository.

---

## Background: Why Installation Fails on Windows

BlueMuse is distributed as a locally installed MSIX package with PowerShell installer scripts (`InstallBlueMuse.ps1` / `Install.ps1` / `Add-AppDevPackage.ps1`). Each release provides two separate architecture-specific artifacts (x64 and x86) - download and unzip the one matching your machine before following the steps below.

Windows enforces **two separate** security systems that often block installation:

1. **Windows Developer Mode** (AppX trust requirement).
2. **PowerShell Execution Policy** (script execution control).

Both must be handled correctly.

---

## Windows Developer Mode

### Definition

Developer Mode allows Windows to install locally packaged AppX/MSIX applications that are not installed via the Microsoft Store.

### Why BlueMuse Needs It

BlueMuse is not a Store app. Without Developer Mode, Windows refuses to register the package and attempts to acquire a "developer license", which no longer exists.

### Common Error

```
Error: Could not acquire a developer license.
```

**Meaning:** This error indicates Developer Mode is disabled.

### How to Enable

1. Open Windows Settings.
2. Go to **Update & Security → For developers**.
3. Select **"Developer mode"**.
4. Accept the warning.

> **Note:** Modern Windows uses Developer Mode instead of the old developer license system. The error message is legacy wording.

---

## PowerShell Execution Policy

### Definition

Execution policies control whether PowerShell scripts are allowed to run.

### Common Policies

| Policy         | Description                       |
|----------------|-----------------------------------|
| `Restricted`   | No scripts allowed.               |
| `RemoteSigned` | Downloaded scripts must be signed. |
| `Bypass`       | No restrictions.                   |

Even with `RemoteSigned`, scripts downloaded from GitHub are blocked by default.

### Common Error

```
running scripts is disabled on this system
```

**Meaning:** PowerShell is blocking the installer script from executing.

---

## Unblocking Downloaded Scripts

### Why This Is Required

Windows marks downloaded files with a "Mark of the Web" flag. PowerShell refuses to run scripts with this flag.

### Command

```powershell
Unblock-File -Path ".\Add-AppDevPackage.ps1"
```

**Effect:** Removes the downloaded-file restriction without changing execution policy.

---

## Temporary Execution Policy Bypass (Recommended)

> **Important:** Do NOT change execution policy system-wide.

### Correct Approach

Bypass execution policy ONLY for the current PowerShell session.

### Command

```powershell
Set-ExecutionPolicy Bypass -Scope Process
```

### Details

- Applies only to the current PowerShell window.
- Automatically resets when the window closes.
- Safe and reversible.

---

## Complete Installation Steps

1. **Enable Developer Mode.**

   `Settings → Update & Security → For developers → Developer mode`

2. **Open PowerShell as Administrator.**

3. **Navigate to the unzipped BlueMuse folder for your architecture.**
   ```powershell
   cd "C:\Users\<username>\Downloads\BlueMuse.App_2.5.0.0_x64"
   ```

4. **Temporarily bypass execution policy.**
   ```powershell
   Set-ExecutionPolicy Bypass -Scope Process
   ```

5. **Unblock the installer script.**
   ```powershell
   Unblock-File -Path ".\InstallBlueMuse.ps1"
   ```

6. **Run the installer.**
   ```powershell
   .\InstallBlueMuse.ps1
   ```

7. **When prompted:**
   ```
   [D] Do not run  [R] Run once  [S] Suspend
   ```
   Type `R` and press Enter.

---

## Common Prompts and Errors

| Error                                                        | Cause                                                  |
|--------------------------------------------------------------|--------------------------------------------------------|
| `File cannot be loaded because running scripts is disabled`  | Execution policy blocking script.                      |
| `Could not acquire a developer license`                      | Developer Mode not enabled.                            |
| Script prompts but exits immediately                         | Script still blocked or execution policy not bypassed. |

---

## Verification

After successful installation:

### Launch

```
BlueMuse.exe
```

### Expected Behavior

- BlueMuse GUI opens without errors.
- App launches even if no Muse device is powered on.
- LSL services initialize when streaming starts.

---

## Why This Matters for LSL/BCI Workflows

BlueMuse provides a GUI-based LSL EEG stream source.

### Benefits

- No Python required for device connection.
- EEG streams appear immediately on the LSL network.
- Python (MNE, BrainFlow) can subscribe independently.
- Unity/Unreal can consume derived command streams.

This separation enables modular, scalable BCI pipelines.

---

## Summary

- Developer Mode is required for local AppX installation.
- Downloaded scripts must be unblocked.
- `ExecutionPolicy Bypass` (Process scope) enables safe script execution.
- No permanent system security changes are required.
