"use client";

import { useCallback, useEffect, useMemo, useState, type ReactNode } from "react";
import { usePathname, useRouter } from "next/navigation";
import { LoginRedirectLoading, WorkspaceLoadingShell } from "@/features/app-shell/components/WorkspaceLoadingShell";
import { WorkspaceSidebar } from "@/features/app-shell/components/WorkspaceSidebar";
import { WorkspaceTopbar } from "@/features/app-shell/components/WorkspaceTopbar";
import { navItems, type NavGroupId } from "@/features/app-shell/navigation";
import { ForcedPasswordChangeView } from "@/features/app-shell/views/ForcedPasswordChangeView";
import { useSessionStore } from "@/features/session/sessionStore";
import { api } from "@/lib/api";

function canUseNavItem(userPermissions: string[], requiredPermissions?: string[]) {
  if (!requiredPermissions?.length) {
    return true;
  }

  return requiredPermissions.some((permission) => userPermissions.includes(permission));
}

export default function WorkspaceLayout({ children }: { children: ReactNode }) {
  const router = useRouter();
  const pathname = usePathname();
  const {
    user,
    token,
    expiresAt,
    theme,
    language,
    hasHydrated,
    hasCheckedSession,
    logout,
    setUser,
    toggleLanguage,
    toggleTheme,
  } = useSessionStore();
  const [isMobileNavOpen, setIsMobileNavOpen] = useState(false);
  const [openNavGroups, setOpenNavGroups] = useState<NavGroupId[]>([]);

  const visibleNavItems = useMemo(
    () =>
      navItems.filter((item) => {
        if (!user || !canUseNavItem(user.permissions ?? [], item.permissions)) {
          return false;
        }

        return user.mustChangePassword ? item.viewId === "settings" : true;
      }),
    [user],
  );

  const currentViewId = navItems.find((item) => item.path === pathname)?.viewId;
  const canAccessCurrentRoute = visibleNavItems.some((item) => item.viewId === currentViewId);
  const canAccessTasks = visibleNavItems.some((item) => item.viewId === "tasks");
  const toggleNavGroup = useCallback((groupId: NavGroupId) => {
    setOpenNavGroups((current) =>
      current.includes(groupId) ? current.filter((candidate) => candidate !== groupId) : [...current, groupId],
    );
  }, []);

  const endSession = useCallback(() => {
    logout();
    router.replace("/login");
  }, [logout, router]);

  const requestLogout = useCallback(() => {
    if (token && !token.startsWith("demo-")) {
      void api.logout(token).finally(endSession);
      return;
    }

    endSession();
  }, [endSession, token]);

  useEffect(() => {
    if (!isMobileNavOpen) {
      return;
    }

    function closeOnEscape(event: KeyboardEvent) {
      if (event.key === "Escape") {
        setIsMobileNavOpen(false);
      }
    }

    window.addEventListener("keydown", closeOnEscape);
    return () => window.removeEventListener("keydown", closeOnEscape);
  }, [isMobileNavOpen]);

  useEffect(() => {
    if (!hasHydrated || !hasCheckedSession || !user || user.mustChangePassword || canAccessCurrentRoute) {
      return;
    }

    router.replace(visibleNavItems[0]?.path ?? "/dashboard");
  }, [canAccessCurrentRoute, hasCheckedSession, hasHydrated, router, user, visibleNavItems]);

  useEffect(() => {
    if (hasHydrated && hasCheckedSession && !user) {
      router.replace("/login");
    }
  }, [hasCheckedSession, hasHydrated, router, user]);

  return (
    <>
      {!hasHydrated || !hasCheckedSession ? <WorkspaceLoadingShell /> : null}
      {hasHydrated && hasCheckedSession && !user ? <LoginRedirectLoading /> : null}
      {hasHydrated && hasCheckedSession && user?.mustChangePassword ? (
        <ForcedPasswordChangeView
          language={language}
          token={token}
          user={user}
          onLogout={endSession}
          onUserUpdated={setUser}
          onToggleLanguage={toggleLanguage}
          onToggleTheme={toggleTheme}
          theme={theme}
        />
      ) : null}
      {hasHydrated && hasCheckedSession && user && !user.mustChangePassword && canAccessCurrentRoute ? (
        <div className="app-shell">
          <WorkspaceSidebar
            isMobileOpen={isMobileNavOpen}
            items={visibleNavItems}
            language={language}
            openGroups={openNavGroups}
            pathname={pathname}
            onCloseMobile={() => setIsMobileNavOpen(false)}
            onToggleGroup={toggleNavGroup}
          />
          <div className={pathname === "/forms" ? "main-area main-area-designer" : "main-area"}>
            <WorkspaceTopbar
              canAccessTasks={canAccessTasks}
              expiresAt={expiresAt}
              isMobileNavOpen={isMobileNavOpen}
              language={language}
              theme={theme}
              token={token}
              user={user}
              onLogout={requestLogout}
              onToggleLanguage={toggleLanguage}
              onToggleMobileNav={() => setIsMobileNavOpen((isOpen) => !isOpen)}
              onToggleTheme={toggleTheme}
            />
            <main className={pathname === "/forms" ? "content content-designer-wide" : "content"}>
              <div className="workspace-route-content" key={pathname}>{children}</div>
            </main>
          </div>
        </div>
      ) : null}
    </>
  );
}
