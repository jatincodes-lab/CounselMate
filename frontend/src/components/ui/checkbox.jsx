import React from "react";

export function Checkbox({ className = "", ...props }) {
  return <input className={`ui-checkbox ${className}`.trim()} type="checkbox" {...props} />;
}
