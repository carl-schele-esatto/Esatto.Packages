const e = [
  {
    type: "propertyEditorUi",
    alias: "Backoffice.CustomEditors.MaskedTextBox",
    name: "Masked Text Box",
    element: () => import("./masked-text-box.element-BSHgUsB4.js"),
    meta: {
      label: "Masked Text Box",
      icon: "icon-lock",
      group: "common",
      // Built-in string schema, so this UI is also pickable on content data types.
      propertyEditorSchemaAlias: "Umbraco.Plain.String",
      settings: {
        properties: [
          {
            alias: "mask",
            label: "Mask value",
            description: "Hide the value (e.g. for API keys / secrets) behind a reveal toggle.",
            propertyEditorUiAlias: "Umb.PropertyEditorUi.Toggle"
          }
        ],
        defaultData: [{ alias: "mask", value: !0 }]
      }
    }
  }
], t = [
  ...e
];
export {
  t as manifests
};
//# sourceMappingURL=backoffice-custom-editors.js.map
