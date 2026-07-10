"use client";

import { WorkspaceShell } from "@/features/app-shell/WorkspaceShell";
import { SettingsView } from "@/features/app-shell/views/SettingsView";

export default function SettingsPage() {
  return <WorkspaceShell viewId="settings">{({ expiresAt, language, token, user, logout, setUser }) => <SettingsView expiresAt={expiresAt} language={language} token={token} user={user} onLogout={logout} onUserUpdated={setUser} />}</WorkspaceShell>;
}
