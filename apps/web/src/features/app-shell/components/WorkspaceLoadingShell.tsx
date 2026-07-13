import { PrototypeLogo } from "@/features/app-shell/PrototypeLogo";

export function WorkspaceLoadingShell() {
  return (
    <div className="app-shell workspace-loading-shell" aria-live="polite" aria-label="Calisma alani yukleniyor">
      <aside className="sidebar workspace-loading-sidebar">
        <div className="brand">
          <span className="brand-symbol"><PrototypeLogo size={34} /></span>
          <div><strong>TechYouth BPM</strong><span>Workspace</span></div>
        </div>
        <div className="workspace-loading-nav"><span /><span /><span /><span /><span /></div>
      </aside>
      <div className="main-area">
        <header className="topbar workspace-loading-topbar"><span /><span /></header>
        <main className="content"><div className="workspace-loading-content"><span /><span /><span /></div></main>
      </div>
    </div>
  );
}

export function LoginRedirectLoading() {
  return (
    <main className="login-page" aria-live="polite">
      <section className="login-panel session-loading">
        <PrototypeLogo size={44} />
        <span className="eyebrow">Oturum</span>
        <h1>Giris ekranina yonlendiriliyor</h1>
      </section>
    </main>
  );
}
