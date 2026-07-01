---
name: Kaval Online — 2FA Experience
status: final
created: 2026-07-01
updated: 2026-07-01
colors:
  # Inherits Angular Material theme. Only 2FA-specific semantic tokens added.
  enrolled: '#2E7D32'
  not-enrolled: '#C62828'
  warning: '#E65100'
  info-banner: '#1565C0'
typography:
  # Inherits Angular Material typography. No overrides.
rounded:
  # Inherits Material defaults (4px). No overrides.
spacing:
  # Inherits Material spacing. No overrides.
components:
  badge-2fa:
    background: '{colors.enrolled}'
    foreground: '#FFFFFF'
    radius: '{rounded.sm}'
  banner-warning:
    background: '#FFF3E0'
    foreground: '{colors.warning}'
    border: '{colors.warning}'
  qr-container:
    background: '#FFFFFF'
    radius: 8px
    padding: 16px
  totp-input:
    width: '200px'
    fontFamily: 'monospace'
    fontSize: '24px'
    letterSpacing: '0.5em'
  backup-code-chip:
    fontFamily: 'monospace'
    fontSize: '14px'
    background: '#F5F5F5'
    radius: 4px
    padding: '8px 12px'
---
