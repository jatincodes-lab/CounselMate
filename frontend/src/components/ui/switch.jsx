import React from "react";

export function Switch({ checked = false, className = "", onCheckedChange, disabled, ...props }) {
  return (
    <button
      type="button"
      role="switch"
      aria-checked={checked}
      className={`ui-switch ${checked ? "is-checked" : ""} ${className}`.trim()}
      disabled={disabled}
      onClick={() => onCheckedChange?.(!checked)}
      {...props}
    >
      <span className="ui-switch-thumb" />
    </button>
  );
}
