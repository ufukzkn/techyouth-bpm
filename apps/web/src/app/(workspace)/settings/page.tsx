"use client";

import { useCallback } from "react";
import { useRouter } from "next/navigation";
import { SettingsView } from "@/features/app-shell/views/SettingsView";
import { useSessionStore } from "@/features/session/sessionStore";

export default function SettingsPage() {
  const { user, token, expiresAt, language, logout, setUser } = useSessionStore();
  const router = useRouter();

  const handleLogout = useCallback(() => {
    logout();
    router.replace("/login");
  }, [logout, router]);

  if (!user) return null;

  return (
    <SettingsView
      expiresAt={expiresAt}
      language={language}
      token={token}
      user={user}
      onLogout={handleLogout}
      onUserUpdated={setUser}
    />
  );
}
