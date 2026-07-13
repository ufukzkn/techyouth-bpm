"use client";

import { Blocks, X, type LucideIcon } from "lucide-react";
import { useCallback, useEffect, useMemo, useRef, useState, useSyncExternalStore, type CSSProperties, type PointerEvent as ReactPointerEvent } from "react";
import { createPortal } from "react-dom";
import type { FieldType } from "@/lib/types";

const storageKey = "techyouth-form-palette-fab-position";
const buttonSize = 54;
const safeInset = 14;
const safeTop = 78;
const dragThreshold = 7;
const sheetCloseDistance = 72;
const sheetCloseVelocity = 0.45;
const defaultPosition: StoredPosition = { edge: "right", yRatio: 0.82 };
const subscribeToHydration = () => () => undefined;

type StoredPosition = {
  edge: "left" | "right";
  yRatio: number;
};

type Point = { x: number; y: number };
type SheetPhase = "opening" | "open" | "closing";

type MobileFieldPaletteProps = {
  closeLabel: string;
  description: string;
  items: Array<{
    description: string;
    icon: LucideIcon;
    label: string;
    type: FieldType;
  }>;
  onSelect: (fieldType: FieldType) => void;
  openLabel: string;
  title: string;
};

export function MobileFieldPalette({
  closeLabel,
  description,
  items,
  onSelect,
  openLabel,
  title,
}: MobileFieldPaletteProps) {
  const mounted = useSyncExternalStore(subscribeToHydration, () => true, () => false);
  const [isOpen, setIsOpen] = useState(false);
  const [sheetPhase, setSheetPhase] = useState<SheetPhase>("opening");
  const [isSheetDragging, setIsSheetDragging] = useState(false);
  const [position, setPosition] = useState<StoredPosition>(() =>
    typeof window === "undefined" ? defaultPosition : readStoredPosition() ?? defaultPosition,
  );
  const [draftPoint, setDraftPoint] = useState<Point | null>(null);
  const [viewport, setViewport] = useState(() =>
    typeof window === "undefined" ? { width: 390, height: 844 } : { width: window.innerWidth, height: window.innerHeight },
  );
  const [sheetDragY, setSheetDragY] = useState(0);
  const bubbleRef = useRef<HTMLButtonElement>(null);
  const closeRef = useRef<HTMLButtonElement>(null);
  const sheetRef = useRef<HTMLElement>(null);
  const bubbleDragRef = useRef<{ pointerId: number; start: Point; origin: Point; hasDragged: boolean } | null>(null);
  const suppressNextClickRef = useRef(false);
  const sheetDragRef = useRef<{
    pointerId: number;
    startY: number;
    startAt: number;
    lastY: number;
    lastAt: number;
  } | null>(null);

  useEffect(() => {
    function updateViewport() {
      setViewport({ width: window.innerWidth, height: window.innerHeight });
      if (window.innerWidth > 860) {
        setIsOpen(false);
        setSheetPhase("opening");
        setIsSheetDragging(false);
        setSheetDragY(0);
      }
    }

    window.addEventListener("resize", updateViewport);
    return () => window.removeEventListener("resize", updateViewport);
  }, []);

  const finishClose = useCallback(() => {
    setIsOpen(false);
    setSheetPhase("opening");
    setIsSheetDragging(false);
    setSheetDragY(0);
  }, []);

  const requestClose = useCallback(() => {
    if (window.matchMedia("(prefers-reduced-motion: reduce)").matches) {
      finishClose();
      return;
    }

    setIsSheetDragging(false);
    setSheetPhase("closing");
  }, [finishClose]);

  useEffect(() => {
    if (!isOpen || sheetPhase !== "opening") {
      return;
    }

    const reduceMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    let secondFrame = 0;
    const firstFrame = window.requestAnimationFrame(() => {
      if (reduceMotion) {
        setSheetPhase("open");
        return;
      }
      secondFrame = window.requestAnimationFrame(() => setSheetPhase("open"));
    });
    return () => {
      window.cancelAnimationFrame(firstFrame);
      window.cancelAnimationFrame(secondFrame);
    };
  }, [isOpen, sheetPhase]);

  useEffect(() => {
    if (sheetPhase !== "closing") {
      return;
    }

    const fallback = window.setTimeout(finishClose, 500);
    return () => window.clearTimeout(fallback);
  }, [finishClose, sheetPhase]);

  useEffect(() => {
    if (!isOpen) {
      return;
    }

    const previousOverflow = document.body.style.overflow;
    const bubble = bubbleRef.current;
    document.body.style.overflow = "hidden";
    closeRef.current?.focus();

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === "Escape") {
        requestClose();
      }
    }

    window.addEventListener("keydown", handleKeyDown);
    return () => {
      document.body.style.overflow = previousOverflow;
      window.removeEventListener("keydown", handleKeyDown);
      bubble?.focus();
    };
  }, [isOpen, requestClose]);

  const restingPoint = useMemo(() => positionToPoint(position, viewport), [position, viewport]);
  const bubblePoint = draftPoint ?? restingPoint;
  const bubbleStyle: CSSProperties = { left: bubblePoint.x, top: bubblePoint.y };

  function handleBubblePointerDown(event: ReactPointerEvent<HTMLButtonElement>) {
    if (event.pointerType === "mouse" && event.button !== 0) {
      return;
    }

    event.currentTarget.setPointerCapture(event.pointerId);
    bubbleDragRef.current = {
      pointerId: event.pointerId,
      start: { x: event.clientX, y: event.clientY },
      origin: bubblePoint,
      hasDragged: false,
    };
  }

  function handleBubblePointerMove(event: ReactPointerEvent<HTMLButtonElement>) {
    const drag = bubbleDragRef.current;
    if (!drag || drag.pointerId !== event.pointerId) {
      return;
    }

    const distance = Math.hypot(event.clientX - drag.start.x, event.clientY - drag.start.y);
    if (!drag.hasDragged && distance < dragThreshold) {
      return;
    }

    drag.hasDragged = true;
    setDraftPoint(clampPoint({
      x: drag.origin.x + event.clientX - drag.start.x,
      y: drag.origin.y + event.clientY - drag.start.y,
    }, viewport));
  }

  function handleBubblePointerUp(event: ReactPointerEvent<HTMLButtonElement>) {
    const drag = bubbleDragRef.current;
    if (!drag || drag.pointerId !== event.pointerId) {
      return;
    }

    bubbleDragRef.current = null;
    if (!drag.hasDragged) {
      setDraftPoint(null);
      return;
    }

    suppressNextClickRef.current = true;
    const point = clampPoint({
      x: drag.origin.x + event.clientX - drag.start.x,
      y: drag.origin.y + event.clientY - drag.start.y,
    }, viewport);
    const nextPosition = pointToStoredPosition(point, viewport);
    setPosition(nextPosition);
    setDraftPoint(null);
    window.localStorage.setItem(storageKey, JSON.stringify(nextPosition));
  }

  function resetBubbleDrag() {
    bubbleDragRef.current = null;
    setDraftPoint(null);
  }

  function handleSheetPointerDown(event: ReactPointerEvent<HTMLDivElement>) {
    event.currentTarget.setPointerCapture(event.pointerId);
    const now = performance.now();
    sheetDragRef.current = {
      pointerId: event.pointerId,
      startY: event.clientY,
      startAt: now,
      lastY: event.clientY,
      lastAt: now,
    };
    setIsSheetDragging(true);
  }

  function handleSheetPointerMove(event: ReactPointerEvent<HTMLDivElement>) {
    const drag = sheetDragRef.current;
    if (drag?.pointerId === event.pointerId) {
      drag.lastY = event.clientY;
      drag.lastAt = performance.now();
      setSheetDragY(Math.max(0, event.clientY - drag.startY));
    }
  }

  function handleSheetPointerUp(event: ReactPointerEvent<HTMLDivElement>) {
    const drag = sheetDragRef.current;
    if (!drag || drag.pointerId !== event.pointerId) {
      return;
    }

    const now = performance.now();
    const releasedDistance = Math.max(0, event.clientY - drag.startY);
    const sampleDistance = Math.max(0, event.clientY - drag.lastY);
    const sampleDuration = Math.max(1, now - drag.lastAt);
    const totalVelocity = releasedDistance / Math.max(1, now - drag.startAt);
    const releaseVelocity = Math.max(sampleDistance / sampleDuration, totalVelocity);
    sheetDragRef.current = null;
    setIsSheetDragging(false);
    if (releasedDistance >= sheetCloseDistance || (releasedDistance >= 18 && releaseVelocity >= sheetCloseVelocity)) {
      setSheetDragY(releasedDistance);
      requestClose();
      return;
    }
    setSheetDragY(0);
  }

  function resetSheetDrag() {
    if (!sheetDragRef.current) {
      return;
    }
    sheetDragRef.current = null;
    setIsSheetDragging(false);
    setSheetDragY(0);
  }

  function openSheet() {
    setSheetDragY(0);
    setSheetPhase("opening");
    setIsOpen(true);
  }

  if (!mounted) {
    return null;
  }

  return createPortal(
    <>
      <button
        aria-expanded={isOpen && sheetPhase !== "closing"}
        aria-haspopup="dialog"
        aria-label={openLabel}
        className="mobile-field-palette-fab"
        onClick={(event) => {
          if (suppressNextClickRef.current) {
            suppressNextClickRef.current = false;
            event.preventDefault();
            return;
          }
          openSheet();
        }}
        onLostPointerCapture={resetBubbleDrag}
        onPointerCancel={resetBubbleDrag}
        onPointerDown={handleBubblePointerDown}
        onPointerMove={handleBubblePointerMove}
        onPointerUp={handleBubblePointerUp}
        ref={bubbleRef}
        style={bubbleStyle}
        title={openLabel}
        type="button"
      >
        <Blocks size={22} aria-hidden="true" />
      </button>

      {isOpen ? (
        <div className={`mobile-field-palette-layer mobile-field-palette-layer-${sheetPhase}`}>
          <button className="mobile-field-palette-backdrop" aria-label={closeLabel} onClick={requestClose} type="button" />
          <section
            aria-labelledby="mobile-field-palette-title"
            aria-modal="true"
            className={`mobile-field-palette-sheet mobile-field-palette-sheet-${sheetPhase}${
              isSheetDragging ? " mobile-field-palette-sheet-dragging" : ""
            }`}
            onKeyDown={(event) => {
              if (event.key !== "Tab") {
                return;
              }
              const focusable = sheetRef.current?.querySelectorAll<HTMLElement>("button:not([disabled])");
              if (!focusable?.length) {
                return;
              }
              const first = focusable[0];
              const last = focusable[focusable.length - 1];
              if (event.shiftKey && document.activeElement === first) {
                event.preventDefault();
                last.focus();
              } else if (!event.shiftKey && document.activeElement === last) {
                event.preventDefault();
                first.focus();
              }
            }}
            ref={sheetRef}
            role="dialog"
            style={{ "--sheet-drag-y": `${sheetDragY}px` } as CSSProperties}
            onTransitionEnd={(event) => {
              if (event.target === event.currentTarget && event.propertyName === "transform" && sheetPhase === "closing") {
                finishClose();
              }
            }}
          >
            <div
              aria-label={closeLabel}
              className="mobile-field-palette-drag-zone"
              onLostPointerCapture={resetSheetDrag}
              onPointerCancel={resetSheetDrag}
              onPointerDown={handleSheetPointerDown}
              onPointerMove={handleSheetPointerMove}
              onPointerUp={handleSheetPointerUp}
            >
              <span className="mobile-field-palette-handle" aria-hidden="true" />
            </div>
            <div className="mobile-field-palette-heading">
              <div>
                <h2 id="mobile-field-palette-title">{title}</h2>
                <p>{description}</p>
              </div>
              <button className="icon-button" aria-label={closeLabel} onClick={requestClose} ref={closeRef} type="button">
                <X size={18} />
              </button>
            </div>
            <div className="mobile-field-palette-options">
              {items.map((item) => (
                <button
                  className="mobile-field-palette-option"
                  key={item.type}
                  onClick={() => {
                    onSelect(item.type);
                    requestClose();
                  }}
                  type="button"
                >
                  <item.icon size={20} aria-hidden="true" />
                  <span>
                    <strong>{item.label}</strong>
                    <small>{item.description}</small>
                  </span>
                </button>
              ))}
            </div>
          </section>
        </div>
      ) : null}
    </>,
    document.body,
  );
}

function clampPoint(point: Point, viewport: { width: number; height: number }): Point {
  return {
    x: Math.min(Math.max(point.x, safeInset), Math.max(safeInset, viewport.width - buttonSize - safeInset)),
    y: Math.min(Math.max(point.y, safeTop), Math.max(safeTop, viewport.height - buttonSize - safeInset)),
  };
}

function positionToPoint(position: StoredPosition, viewport: { width: number; height: number }): Point {
  const availableHeight = Math.max(1, viewport.height - safeTop - buttonSize - safeInset);
  return clampPoint({
    x: position.edge === "left" ? safeInset : viewport.width - buttonSize - safeInset,
    y: safeTop + availableHeight * Math.min(Math.max(position.yRatio, 0), 1),
  }, viewport);
}

function pointToStoredPosition(point: Point, viewport: { width: number; height: number }): StoredPosition {
  const availableHeight = Math.max(1, viewport.height - safeTop - buttonSize - safeInset);
  return {
    edge: point.x + buttonSize / 2 < viewport.width / 2 ? "left" : "right",
    yRatio: Math.min(Math.max((point.y - safeTop) / availableHeight, 0), 1),
  };
}

function readStoredPosition(): StoredPosition | null {
  try {
    const value = JSON.parse(window.localStorage.getItem(storageKey) ?? "null") as Partial<StoredPosition> | null;
    return value && (value.edge === "left" || value.edge === "right") && typeof value.yRatio === "number"
      ? { edge: value.edge, yRatio: value.yRatio }
      : null;
  } catch {
    return null;
  }
}
