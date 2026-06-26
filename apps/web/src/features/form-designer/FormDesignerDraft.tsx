"use client";

import { Plus, Save, Trash2 } from "lucide-react";
import { useMemo, useState } from "react";
import { useSessionStore } from "@/features/session/sessionStore";
import { api, ApiError } from "@/lib/api";
import type { CreateFormRequest, FieldType, FormFieldDefinition } from "@/lib/types";

type DesignerField = Omit<FormFieldDefinition, "id"> & {
  id: string;
};

const initialFields: DesignerField[] = [
  {
    id: "customerName",
    key: "customerName",
    label: "Musteri adi",
    type: "Text",
    required: true,
    sortOrder: 1,
    options: [],
    validationRules: [],
  },
  {
    id: "requestType",
    key: "requestType",
    label: "Talep tipi",
    type: "Select",
    required: true,
    sortOrder: 2,
    options: ["Izin", "Masraf", "Satinalma"],
    validationRules: [],
  },
  {
    id: "approvalNote",
    key: "approvalNote",
    label: "Onay aciklamasi",
    type: "Text",
    required: false,
    sortOrder: 3,
    options: [],
    validationRules: [
      {
        ruleType: "RequiredWhen",
        dependsOnFieldKey: "requestType",
        expectedValue: "Satinalma",
        message: "Satinalma taleplerinde onay aciklamasi zorunludur.",
      },
    ],
  },
];

export function FormDesignerDraft() {
  const token = useSessionStore((state) => state.token);
  const [fields, setFields] = useState<DesignerField[]>(initialFields);
  const [formName, setFormName] = useState("Demo Surec Formu");
  const [description, setDescription] = useState("Frontend tarafinda tasarlanan form modeli");
  const [label, setLabel] = useState("Masraf merkezi");
  const [type, setType] = useState<FieldType>("Text");
  const [required, setRequired] = useState(false);
  const [saveState, setSaveState] = useState<"idle" | "saving" | "success" | "error">("idle");
  const [message, setMessage] = useState("Form henuz kaydedilmedi.");

  const formModel = useMemo<CreateFormRequest>(
    () => ({
      name: formName,
      description,
      fields: fields.map((field, index) => ({
        key: field.key,
        label: field.label,
        type: field.type,
        required: field.required,
        sortOrder: index + 1,
        options: field.options,
        validationRules: field.validationRules,
      })),
    }),
    [description, fields, formName],
  );

  function addField() {
    const safeKey = label
      .trim()
      .toLowerCase()
      .replace(/[^a-z0-9]+/g, " ")
      .trim()
      .replace(/\s+([a-z0-9])/g, (_, char: string) => char.toUpperCase());

    const key = safeKey || `field${fields.length + 1}`;
    const nextField: DesignerField = {
      id: `${key}-${Date.now()}`,
      key,
      label: label.trim() || `Alan ${fields.length + 1}`,
      type,
      required,
      sortOrder: fields.length + 1,
      options: type === "Select" ? ["Secenek A", "Secenek B"] : [],
      validationRules: [],
    };

    setFields((current) => [...current, nextField]);
    setLabel("");
    setType("Text");
    setRequired(false);
    setSaveState("idle");
    setMessage("Formda kaydedilmemis degisiklikler var.");
  }

  function removeField(id: string) {
    setFields((current) =>
      current.filter((field) => field.id !== id).map((field, index) => ({ ...field, sortOrder: index + 1 })),
    );
    setSaveState("idle");
    setMessage("Formda kaydedilmemis degisiklikler var.");
  }

  function toggleRequired(id: string) {
    setFields((current) =>
      current.map((field) => (field.id === id ? { ...field, required: !field.required } : field)),
    );
    setSaveState("idle");
    setMessage("Formda kaydedilmemis degisiklikler var.");
  }

  async function saveForm() {
    if (!token) {
      setSaveState("error");
      setMessage("Form kaydetmek icin API oturumu gerekli.");
      return;
    }

    try {
      setSaveState("saving");
      const saved = await api.createForm(token, formModel);
      setSaveState("success");
      setMessage(`Form SQLite veritabanina kaydedildi: ${saved.name}`);
    } catch (error) {
      setSaveState("error");
      setMessage(error instanceof ApiError ? error.errors.join(" ") : "Form kaydedilemedi.");
    }
  }

  return (
    <section className="designer-section">
      <div className="section-heading">
        <div>
          <span className="eyebrow">Form Tasarimi</span>
          <h2>Dinamik form modeli</h2>
        </div>
        <p>Alanlar UI tarafinda tasarlanir ve backend tarafinda form definition olarak saklanir.</p>
      </div>

      <div className="designer-grid">
        <div className="tool-panel">
          <h3>Form bilgisi</h3>
          <label>
            Form adi
            <input value={formName} onChange={(event) => setFormName(event.target.value)} />
          </label>
          <label>
            Aciklama
            <input value={description} onChange={(event) => setDescription(event.target.value)} />
          </label>
          <button className="primary-button" disabled={saveState === "saving"} type="button" onClick={saveForm}>
            <Save size={18} />
            {saveState === "saving" ? "Kaydediliyor" : "Formu kaydet"}
          </button>
          <p className={`status-line status-line-${saveState}`}>{message}</p>
        </div>

        <div className="tool-panel">
          <h3>Alan ekle</h3>
          <label>
            Etiket
            <input value={label} onChange={(event) => setLabel(event.target.value)} placeholder="Alan etiketi" />
          </label>
          <label>
            Tip
            <select value={type} onChange={(event) => setType(event.target.value as FieldType)}>
              <option value="Text">Text</option>
              <option value="Number">Number</option>
              <option value="Email">Email</option>
              <option value="Select">Select</option>
              <option value="Checkbox">Checkbox</option>
              <option value="Date">Date</option>
            </select>
          </label>
          <label className="checkbox-row">
            <input checked={required} onChange={(event) => setRequired(event.target.checked)} type="checkbox" />
            Zorunlu alan
          </label>
          <button className="secondary-button" type="button" onClick={addField}>
            <Plus size={18} />
            Alan ekle
          </button>
        </div>

        <div className="field-list" aria-label="Designed fields">
          {fields.map((field) => (
            <article className="field-card" key={field.id}>
              <div>
                <strong>{field.label}</strong>
                <span>
                  {field.key} - {field.type} - Sira {field.sortOrder}
                </span>
              </div>
              <label className="compact-toggle">
                <input checked={field.required} onChange={() => toggleRequired(field.id)} type="checkbox" />
                Zorunlu
              </label>
              <button className="icon-button" onClick={() => removeField(field.id)} aria-label={`${field.label} sil`}>
                <Trash2 size={17} />
              </button>
            </article>
          ))}
        </div>

        <pre className="json-preview">{JSON.stringify(formModel, null, 2)}</pre>
      </div>
    </section>
  );
}
