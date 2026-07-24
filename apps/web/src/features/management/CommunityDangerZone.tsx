"use client";

import { AlertTriangle, Archive, Database, Trash2, Users } from "lucide-react";
import { useState } from "react";
import { ActionFeedback } from "@/features/app-shell/components/AsyncState";
import { localizeApiError } from "@/features/i18n/apiErrorMessages";
import { api } from "@/lib/api";
import type {
  Community,
  CommunityDeletionImpact,
  CommunityPurgeResult,
  Language,
} from "@/lib/types";

type Feedback = { tone: "success" | "error" | "loading"; text: string } | null;

export function CommunityDangerZone({
  community,
  language,
  token,
  onPurged,
}: {
  community: Community;
  language: Language;
  token: string;
  onPurged: (result: CommunityPurgeResult) => void;
}) {
  const [impact, setImpact] = useState<CommunityDeletionImpact | null>(null);
  const [dialogStep, setDialogStep] = useState<"impact" | "confirm" | null>(null);
  const [confirmationName, setConfirmationName] = useState("");
  const [currentPassword, setCurrentPassword] = useState("");
  const [reason, setReason] = useState("");
  const [feedback, setFeedback] = useState<Feedback>(null);
  const [isPurging, setIsPurging] = useState(false);

  async function openImpact() {
    if (community.isActive) {
      setFeedback({ tone: "error", text: "Kalıcı silmeden önce topluluğu pasife alın." });
      return;
    }

    setFeedback({ tone: "loading", text: "Silme etkisi hesaplanıyor..." });
    try {
      const result = await api.getCommunityDeletionImpact(token, community.id);
      setImpact(result);
      setDialogStep("impact");
      setFeedback(null);
    } catch (error) {
      setFeedback({
        tone: "error",
        text: localizeApiError(error, language, "Silme etkisi hesaplanamadı."),
      });
    }
  }

  function closeDialog() {
    if (isPurging) {
      return;
    }
    setDialogStep(null);
    setConfirmationName("");
    setCurrentPassword("");
    setReason("");
  }

  async function purgeCommunity() {
    if (!impact || isPurging) {
      return;
    }

    setIsPurging(true);
    setFeedback({ tone: "loading", text: "Topluluk ve operasyonel kayıtlar kalıcı olarak siliniyor..." });
    try {
      const result = await api.purgeCommunity(token, community.id, {
        confirmationName,
        currentPassword,
        reason,
      });
      setDialogStep(null);
      setFeedback({ tone: "success", text: `${result.communityName} silindi; güvenli log arşivi oluşturuldu.` });
      onPurged(result);
    } catch (error) {
      setFeedback({
        tone: "error",
        text: localizeApiError(error, language, "Topluluk kalıcı olarak silinemedi."),
      });
    } finally {
      setIsPurging(false);
      setCurrentPassword("");
    }
  }

  const canConfirm =
    confirmationName === community.name
    && currentPassword.length > 0
    && reason.trim().length >= 10;

  return (
    <>
      <section className="identity-section community-danger-zone">
        <div className="section-toolbar">
          <div>
            <span className="eyebrow">Tehlikeli alan</span>
            <h3>Topluluğu kalıcı olarak sil</h3>
          </div>
          <AlertTriangle aria-hidden="true" size={22} />
        </div>
        <p>
          Bu işlem pasife almaktan farklıdır. Topluluğa bağlı operasyonel kayıtlar geri alınamayacak
          şekilde silinir; yalnız hassas veri içermeyen audit özeti korunur.
        </p>
        <div className="section-actions community-danger-actions">
          <button
            className="danger-button strong-danger-button"
            disabled={community.isActive}
            onClick={() => void openImpact()}
            type="button"
          >
            <Trash2 size={17} />
            Topluluğu kalıcı sil
          </button>
        </div>
        {community.isActive ? (
          <small>Kalıcı silme yalnız pasif topluluklarda kullanılabilir.</small>
        ) : null}
        <ActionFeedback feedback={feedback} />
      </section>

      {dialogStep && impact ? (
        <div className="action-dialog-overlay" onClick={closeDialog}>
          <div
            aria-modal="true"
            className="action-dialog access-confirm-dialog community-purge-dialog"
            onClick={(event) => event.stopPropagation()}
            role="dialog"
          >
            <div className="action-dialog-header">
              <div>
                <span className="eyebrow">Geri alınamaz işlem</span>
                <strong>{dialogStep === "impact" ? "Silme etkisini inceleyin" : community.name}</strong>
              </div>
              <AlertTriangle size={24} />
            </div>

            {dialogStep === "impact" ? (
              <>
                <p className="community-purge-warning">
                  Bu topluluk, kullanıcı bağlantıları, süreçler, işler, formlar ve akışlarla birlikte
                  kalıcı olarak silinecek.
                </p>
                <div className="community-purge-impact-grid">
                  <ImpactMetric icon={Users} label="Kullanıcı" value={impact.userCount} />
                  <ImpactMetric icon={Users} label="Korunacak hesap" value={impact.preservedUserCount} />
                  <ImpactMetric icon={Database} label="Form" value={impact.formCount} />
                  <ImpactMetric icon={Database} label="Akış" value={impact.workflowCount} />
                  <ImpactMetric icon={Database} label="Süreç" value={impact.processCount} />
                  <ImpactMetric icon={Database} label="İş" value={impact.taskCount} />
                  <ImpactMetric icon={Archive} label="Sistem logu" value={impact.systemAuditCount} />
                  <ImpactMetric icon={Archive} label="Süreç adımı" value={impact.processStepCount} />
                </div>
                <div className="action-dialog-actions">
                  <button className="secondary-button" onClick={closeDialog} type="button">Vazgeç</button>
                  <button className="danger-button strong-danger-button" onClick={() => setDialogStep("confirm")} type="button">
                    Etkiyi anladım
                  </button>
                </div>
              </>
            ) : (
              <>
                <p className="community-purge-warning">
                  Tüm topluluk verileri silinecek. Devam etmek için topluluk adını birebir yazın,
                  SuperAdmin parolanızı doğrulayın ve denetlenebilir bir gerekçe girin.
                </p>
                <div className="compact-form community-purge-form">
                  <label>
                    <span>Topluluk adı: <strong>{community.name}</strong></span>
                    <input
                      autoComplete="off"
                      onChange={(event) => setConfirmationName(event.target.value)}
                      placeholder={community.name}
                      value={confirmationName}
                    />
                  </label>
                  <label>
                    <span>Mevcut SuperAdmin parolası</span>
                    <input
                      autoComplete="current-password"
                      onChange={(event) => setCurrentPassword(event.target.value)}
                      type="password"
                      value={currentPassword}
                    />
                  </label>
                  <label>
                    <span>Silme gerekçesi</span>
                    <textarea
                      maxLength={500}
                      onChange={(event) => setReason(event.target.value)}
                      placeholder="En az 10 karakter"
                      rows={4}
                      value={reason}
                    />
                  </label>
                </div>
                <div className="action-dialog-actions">
                  <button className="secondary-button" disabled={isPurging} onClick={() => setDialogStep("impact")} type="button">
                    Geri
                  </button>
                  <button
                    className="danger-button strong-danger-button"
                    disabled={!canConfirm || isPurging}
                    onClick={() => void purgeCommunity()}
                    type="button"
                  >
                    {isPurging ? "Kalıcı olarak siliniyor" : "Topluluğu kalıcı sil"}
                  </button>
                </div>
              </>
            )}
          </div>
        </div>
      ) : null}
    </>
  );
}

function ImpactMetric({
  icon: Icon,
  label,
  value,
}: {
  icon: typeof Users;
  label: string;
  value: number;
}) {
  return (
    <span>
      <Icon aria-hidden="true" size={16} />
      <small>{label}</small>
      <strong>{value}</strong>
    </span>
  );
}
