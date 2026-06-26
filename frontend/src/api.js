const API_BASE_URL = (import.meta.env.VITE_API_BASE_URL || "http://localhost:5078/api").replace(/\/$/, "");
const TENANT_SLUG = import.meta.env.VITE_TENANT_SLUG || "demo-academy";

async function request(path) {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    headers: {
      "X-Tenant-Slug": TENANT_SLUG,
    },
  });

  if (!response.ok) {
    const message = await readErrorMessage(response);
    throw new Error(message || `Request failed with status ${response.status}`);
  }

  return response.json();
}

async function readErrorMessage(response) {
  try {
    const body = await response.json();
    return body.message;
  } catch {
    return "";
  }
}

export async function getCrmData() {
  const [dashboard, leads, pipeline, followUps] = await Promise.all([
    request("/dashboard"),
    request("/leads"),
    request("/pipeline"),
    request("/follow-ups"),
  ]);

  return {
    dashboard,
    leads,
    pipeline,
    followUps,
  };
}
