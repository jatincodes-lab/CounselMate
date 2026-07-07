import React from "react";

export function Card({ className = "", ...props }) {
  return <div className={`ui-card ${className}`.trim()} {...props} />;
}

export function CardHeader({ className = "", ...props }) {
  return <div className={`ui-card-header ${className}`.trim()} {...props} />;
}

export function CardTitle({ className = "", ...props }) {
  return <h3 className={`ui-card-title ${className}`.trim()} {...props} />;
}

export function CardDescription({ className = "", ...props }) {
  return <p className={`ui-card-description ${className}`.trim()} {...props} />;
}

export function CardContent({ className = "", ...props }) {
  return <div className={`ui-card-content ${className}`.trim()} {...props} />;
}

export function CardFooter({ className = "", ...props }) {
  return <div className={`ui-card-footer ${className}`.trim()} {...props} />;
}
