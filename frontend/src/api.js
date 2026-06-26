const API_BASE_URL = (import.meta.env.VITE_API_BASE_URL || "http://localhost:5078/api").replace(/\/$/, "");
const TENANT_SLUG = import.meta.env.VITE_TENANT_SLUG || "demo-academy";

async function request(path, options = {}) {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    method: options.method || "GET",
    headers: {
      "Content-Type": "application/json",
      "X-Tenant-Slug": TENANT_SLUG,
      ...options.headers,
    },
    body: options.body ? JSON.stringify(options.body) : undefined,
  });

  if (!response.ok) {
    throw await createApiError(response);
  }

  return response.json();
}

async function createApiError(response) {
  try {
    const body = await response.json();
    const error = new Error(body.message || body.title || `Request failed with status ${response.status}`);
    error.status = response.status;
    error.errors = body.errors || {};
    return error;
  } catch {
    const error = new Error(`Request failed with status ${response.status}`);
    error.status = response.status;
    error.errors = {};
    return error;
  }
}

export async function getCrmData() {
  const [dashboard, leads, pipeline, followUps, leadOptions] = await Promise.all([
    request("/dashboard"),
    request("/leads"),
    request("/pipeline"),
    request("/follow-ups"),
    request("/leads/options"),
  ]);

  return {
    dashboard,
    leads,
    pipeline,
    followUps,
    leadOptions,
  };
}

export async function createLead(payload) {
  return request("/leads", {
    method: "POST",
    body: payload,
  });
}

export async function getLeadDetail(leadId) {
  return request(`/leads/${encodeURIComponent(leadId)}`);
}

export async function updateLead(leadId, payload) {
  return request(`/leads/${encodeURIComponent(leadId)}`, {
    method: "PATCH",
    body: payload,
  });
}

export async function addLeadActivity(leadId, payload) {
  return request(`/leads/${encodeURIComponent(leadId)}/activities`, {
    method: "POST",
    body: payload,
  });
}

export async function createLeadFollowUp(leadId, payload) {
  return request(`/leads/${encodeURIComponent(leadId)}/follow-ups`, {
    method: "POST",
    body: payload,
  });
}

export async function completeLeadFollowUp(leadId, followUpId) {
  return request(`/leads/${encodeURIComponent(leadId)}/follow-ups/${encodeURIComponent(followUpId)}/complete`, {
    method: "POST",
  });
}
