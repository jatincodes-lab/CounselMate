import React from "react";

export function Select({ className = "", ...props }) {
  return <select className={`ui-select ${className}`.trim()} {...props} />;
}
