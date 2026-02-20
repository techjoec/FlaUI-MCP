# Remote SSH Setup Guide

This guide explains how to use FlaUI-MCP from a Linux/Mac machine when the Windows desktop runs in a VM.

## Architecture

```
┌─────────────────┐                      ┌─────────────────────┐
│  Ubuntu/Mac     │                      │  Windows VM         │
│  (Claude Code)  │──── SSH tunnel ────> │  (FlaUI-MCP)        │
│                 │                      │                     │
│  MCP Client     │     stdio pipe       │  MCP Server         │
└─────────────────┘                      └─────────────────────┘
```

The MCP server runs on Windows and communicates via stdio, piped through SSH.

## Prerequisites

### Windows Side
- Windows 10/11 or Windows Server 2019+
- OpenSSH Server installed and running
- Administrator access (for initial setup)

### Linux/Mac Side
- SSH client (usually pre-installed)
- Key-based SSH authentication configured

---

## Step 1: Install OpenSSH Server on Windows

### Option A: Windows 10/11 (Settings)

1. Open **Settings** > **Apps** > **Optional features**
2. Click **Add a feature**
3. Search for **OpenSSH Server** and install
4. Open **Services** (`services.msc`)
5. Find **OpenSSH SSH Server**, set Startup type to **Automatic**
6. Click **Start** to run it now

### Option B: PowerShell (Administrator)

```powershell
# Install OpenSSH Server
Add-WindowsCapability -Online -Name OpenSSH.Server~~~~0.0.1.0

# Start and enable the service
Start-Service sshd
Set-Service -Name sshd -StartupType 'Automatic'

# Confirm it's running
Get-Service sshd
```

### Option C: Windows Server 2025

OpenSSH Server is built-in. Just enable it:

```powershell
Start-Service sshd
Set-Service -Name sshd -StartupType 'Automatic'
```

---

## Step 2: Configure SSH Key Authentication

### Generate a Key Pair (on Linux/Mac)

```bash
# Generate ED25519 key (recommended)
ssh-keygen -t ed25519 -C "your-email@example.com"

# Or RSA if ED25519 not supported
ssh-keygen -t rsa -b 4096 -C "your-email@example.com"
```

When prompted for a passphrase, press Enter for no passphrase (required for non-interactive use).

### Copy Public Key to Windows

**Option A: Using `ssh-copy-id`**

```bash
ssh-copy-id -i ~/.ssh/id_ed25519.pub username@windows-host
```

**Option B: Manual Copy**

```bash
# On Linux/Mac, display your public key
cat ~/.ssh/id_ed25519.pub

# On Windows, add it to the authorized_keys file
# Run in PowerShell:
New-Item -Path "$env:USERPROFILE\.ssh" -ItemType Directory -Force
Set-Content -Path "$env:USERPROFILE\.ssh\authorized_keys" -Value "ssh-ed25519 AAAA... your-email@example.com"

# Set correct permissions (important!)
icacls "$env:USERPROFILE\.ssh\authorized_keys" /inheritance:r
icacls "$env:USERPROFILE\.ssh\authorized_keys" /grant:r "$env:USERNAME:F"
```

### Test the Connection

```bash
ssh username@windows-host
```

You should log in without a password prompt.

---

## Step 3: Configure SSH Client (Linux/Mac)

Add the Windows host to `~/.ssh/config` for easier access:

```bash
# Edit the config file
nano ~/.ssh/config
```

Add an entry for your Windows VM:

```
Host windows-vm
    HostName 192.168.122.50    # Or your Windows IP/hostname
    User YourWindowsUsername
    IdentityFile ~/.ssh/id_ed25519
    ServerAliveInterval 30
    ServerAliveCountMax 3
```

**Important settings explained:**

- `ServerAliveInterval 30` - Sends keepalive every 30 seconds to prevent connection drops
- `ServerAliveCountMax 3` - Disconnects after 3 failed keepalives (90 seconds)

Now you can connect with just:

```bash
ssh windows-vm
```

---

## Step 4: Build and Deploy FlaUI-MCP

### Build on Windows

```powershell
# Clone the repository
git clone https://github.com/shanselman/FlaUI-MCP.git
cd FlaUI-MCP

# Build
dotnet build src/FlaUI.Mcp

# Note the output path
# Default: src\FlaUI.Mcp\bin\Debug\net10.0-windows\FlaUI.Mcp.dll
```

Or download a release and extract it.

### Note the Full Path

You'll need the full path to `FlaUI.Mcp.dll` for the MCP config. For example:
```
C:\Users\YourName\FlaUI-MCP\src\FlaUI.Mcp\bin\Debug\net10.0-windows\FlaUI.Mcp.dll
```

---

## Step 5: Configure Claude Code

Add the MCP server to your Claude Code configuration.

### Find Your Config File

- **Linux/Mac:** `~/.config/claude-code/mcp-config.json`
- Or run `claude mcp add` for interactive setup

### Configuration

```json
{
  "mcpServers": {
    "flaui": {
      "command": "ssh",
      "args": [
        "-o", "ServerAliveInterval=30",
        "-o", "ServerAliveCountMax=3",
        "windows-vm",
        "dotnet",
        "C:/Users/YourName/FlaUI-MCP/src/FlaUI.Mcp/bin/Debug/net10.0-windows/FlaUI.Mcp.dll"
      ]
    }
  }
}
```

**Replace:**
- `windows-vm` - Your host alias from `~/.ssh/config` (or use IP directly)
- `C:/Users/YourName/...` - Path to FlaUI.Mcp.dll on Windows

### Alternative: Using Host Directly (no ~/.ssh/config)

```json
{
  "mcpServers": {
    "flaui": {
      "command": "ssh",
      "args": [
        "-o", "ServerAliveInterval=30",
        "-o", "ServerAliveCountMax=3",
        "-o", "StrictHostKeyChecking=accept-new",
        "-i", "/home/you/.ssh/id_ed25519",
        "youruser@192.168.122.50",
        "dotnet",
        "C:/path/to/FlaUI.Mcp.dll"
      ]
    }
  }
}
```

---

## Step 6: Test It

In Claude Code:

```
> Use the flaui MCP server to list the available windows
```

You should see a response with the list of windows on the Windows desktop.

---

## Troubleshooting

### Connection Refused

```
ssh: connect to host windows-vm port 22: Connection refused
```

**Solutions:**
1. Ensure OpenSSH Server is running: `Get-Service sshd` (should show "Running")
2. Check Windows Firewall allows SSH (port 22)
3. Verify the IP address is correct

### Permission Denied

```
username@windows-vm: Permission denied (publickey)
```

**Solutions:**
1. Verify your public key is in `~/.ssh/authorized_keys` on Windows
2. Check file permissions on Windows:
   - `.ssh` folder: Only your user should have access
   - `authorized_keys`: Only your user should have access
3. Try connecting manually: `ssh -v windows-vm` (verbose output shows the issue)

### Connection Drops After Inactivity

**Symptom:** MCP calls work, but fail after a few minutes of inactivity.

**Solution:** Ensure `ServerAliveInterval` is set (30 seconds recommended). This keeps the connection alive through NAT/firewall timeouts.

### MCP Server Not Starting

**Check manually:**
```bash
ssh windows-vm "dotnet C:/path/to/FlaUI.Mcp.dll"
```

Then send a test MCP message:
```json
{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{}}}
```

You should get a JSON response. If not, check:
- .NET 10 runtime is installed on Windows
- Path to DLL is correct (use forward slashes or escaped backslashes)

### Path Issues

Windows paths in SSH commands should use:
- **Forward slashes:** `C:/Users/name/path` (recommended)
- **Or escaped backslashes:** `C:\\Users\\name\\path`

### QEMU/Libvirt VMs

If the Windows VM is running under QEMU/libvirt:

1. Ensure the VM network is configured for host access (bridge or user mode with port forward)
2. For user mode (NAT), the VM can access host at `10.0.2.2`
3. For host-to-VM access, may need port forwarding:

```bash
# Forward host port 2222 to VM port 22
# In libvirt XML or using virsh
```

Or use `virt-manager` to configure network settings.

---

## Security Notes

1. **Key-based auth only:** Disable password authentication on Windows for better security:
   ```powershell
   # In C:\ProgramData\ssh\sshd_config, ensure:
   PasswordAuthentication no
   PubkeyAuthentication yes
   ```

2. **Restrict by user:** Only allow specific users to SSH:
   ```powershell
   # In sshd_config:
   AllowUsers yourusername
   ```

3. **No passphrase on keys:** Required for non-interactive MCP use. Protect the private key file permissions.

4. **Network isolation:** If possible, only allow SSH from your host machine's IP.
