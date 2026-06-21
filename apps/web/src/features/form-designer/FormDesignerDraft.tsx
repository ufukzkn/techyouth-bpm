"use client";

import { Plus, Trash2 } from "lucide-react";
import { useMemo, useState } from "react";

type FieldType = "Text" | "Number" | "Email" | "Select" | "Checkbox" | "Date";

type DesignerField = {
  id: string;
  key: string;
  label: string;
  type: FieldType;
  required: boolean;
  sortOrder: number;
  options: string[];
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
  },
  {
    id: "requestType",
    key: "requestType",
    label: "Talep tipi",
    type: "Select",
    required: true,
    sortOrder: 2,
    options: ["Izin", "Masraf", "Satinalma"],
  },
];

export function FormDesignerDraft() {
  const [fields, setFields] = useState<DesignerField[]>(initialFields);
  const [label, setLabel] = useState("Onay aciklamasi");
  const [type, setType] = useState<FieldType>("Text");
  const [required, setRequired] = useState(false);

  const formModel = useMemo(
    () => ({
      name: "Demo Surec Formu",
      description: "Frontend tarafinda tasarlanan form modeli",
      fields: fields.map((field, index) => ({
        key: field.key,
        label: field.label,
        type: field.type,
        required: field.required,
        sortOrder: index + 1,
        options: field.options,
        validationRules:
          field.key === "approvalNote"
            ? [
                {
                  ruleType: "RequiredWhen",
                  dependsOnFieldKey: "requestType",
                  expectedValue: "Satinalma",
                  message: "Satinalma taleplerinde onay aciklamasi zorunludur.",
                },
              ]
            : [],
      })),
    }),
    [fields],
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
    };

    setFields((current) => [...current, nextField]);
    setLabel("");
    setType("Text");
    setRequired(false);
  }

  function removeField(id: string) {
    setFields((current) =>
      current.filter((field) => field.id !== id).map((field, index) => ({ ...field, sortOrder: index + 1 })),
    );
  }

  function toggleRequired(id: string) {
    setFields((current) =>
      current.map((field) => (field.id === id ? { ...field, required: !field.required } : field)),
    );
  }

  return (
    <section className="designer-section" id="forms">
      <div className="section-heading">
        <div>
          <span className="eyebrow">Form Tasarimi</span>
          <h2>Dinamik form modeli</h2>
        </div>
        <p>Alanlar UI tarafinda tasarlanir, backend tarafina form definition modeli olarak gonderilir.</p>
      </div>

      <div className="designer-grid">
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
          <button className="primary-button" type="button" onClick={addField}>
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
                  {field.key} · {field.type} · Sira {field.sortOrder}
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
