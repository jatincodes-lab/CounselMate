const API_BASE_URL = (import.meta.env.VITE_API_BASE_URL || "http://localhost:5078/api").replace(/\/$/, "");
const TENANT_SLUG = import.meta.env.VITE_TENANT_SLUG || "demo-academy";
const TOKEN_STORAGE_KEY = "counselmate_access_token";
const USER_STORAGE_KEY = "counselmate_user";

export function getStoredAuth() {
  const token = localStorage.getItem(TOKEN_STORAGE_KEY);
  const userJson = localStorage.getItem(USER_STORAGE_KEY);

  if (!token || !userJson) {
    return { token: "", user: null };
  }

  try {
    return { token, user: JSON.parse(userJson) };
  } catch {
    clearStoredAuth();
    return { token: "", user: null };
  }
}

export function clearStoredAuth() {
  localStorage.removeItem(TOKEN_STORAGE_KEY);
  localStorage.removeItem(USER_STORAGE_KEY);
}

async function request(path, options = {}) {
  const { token } = getStoredAuth();
  const headers = {
    "Content-Type": "application/json",
    ...options.headers,
  };

  if (token) {
    headers.Authorization = `Bearer ${token}`;
  }

  const response = await fetch(`${API_BASE_URL}${path}`, {
    method: options.method || "GET",
    headers,
    body: options.body ? JSON.stringify(options.body) : undefined,
  });

  if (!response.ok) {
    throw await createApiError(response);
  }

  return response.json();
}

export async function login(payload) {
  const response = await request("/auth/login", {
    method: "POST",
    body: {
      tenantSlug: payload.tenantSlug || TENANT_SLUG,
      email: payload.email,
      password: payload.password,
    },
  });

  localStorage.setItem(TOKEN_STORAGE_KEY, response.token);
  localStorage.setItem(USER_STORAGE_KEY, JSON.stringify(response.user));
  return response;
}

export async function getCurrentUser() {
  return request("/auth/me");
}

export function logout() {
  clearStoredAuth();
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

export async function getPlatformTenants() {
  return request("/platform/tenants");
}

export async function createPlatformTenant(payload) {
  return request("/platform/tenants", {
    method: "POST",
    body: payload,
  });
}

export async function getUsers() {
  return request("/users");
}

export async function createUser(payload) {
  return request("/users", {
    method: "POST",
    body: payload,
  });
}

export async function updateUser(userId, payload) {
  return request(`/users/${encodeURIComponent(userId)}`, {
    method: "PATCH",
    body: payload,
  });
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
