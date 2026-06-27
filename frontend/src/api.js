const API_BASE_URL = (import.meta.env.VITE_API_BASE_URL || "http://localhost:5078/api").replace(/\/$/, "");
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
      email: payload.email,
      password: payload.password,
    },
  });

  localStorage.setItem(TOKEN_STORAGE_KEY, response.token);
  localStorage.setItem(USER_STORAGE_KEY, JSON.stringify(response.user));
  return response;
}

export async function forgotPassword(payload) {
  return request("/auth/forgot-password", {
    method: "POST",
    body: {
      email: payload.email,
    },
  });
}

export async function changePassword(payload) {
  return request("/auth/change-password", {
    method: "POST",
    body: payload,
  });
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
    leads: normalizeLeadList(leads),
    pipeline,
    followUps,
    leadOptions,
  };
}

export async function getLeads(params = {}) {
  const query = new URLSearchParams();
  Object.entries(params).forEach(([key, value]) => {
    if (value !== undefined && value !== null && value !== "") {
      query.set(key, value);
    }
  });

  const response = await request(`/leads${query.toString() ? `?${query}` : ""}`);
  return normalizeLeadList(response);
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

export async function resetUserPassword(userId, payload) {
  return request(`/users/${encodeURIComponent(userId)}/reset-password`, {
    method: "POST",
    body: payload,
  });
}

export async function getMasterData() {
  return request("/master-data");
}

export async function createMasterRecord(type, payload) {
  return request(`/master-data/${encodeURIComponent(type)}`, {
    method: "POST",
    body: payload,
  });
}

export async function updateMasterRecord(type, recordId, payload) {
  return request(`/master-data/${encodeURIComponent(type)}/${encodeURIComponent(recordId)}`, {
    method: "PATCH",
    body: payload,
  });
}

export async function reorderLeadStages(items) {
  return request("/master-data/lead-stages/reorder", {
    method: "POST",
    body: { items },
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

export async function assignLead(leadId, payload) {
  return request(`/leads/${encodeURIComponent(leadId)}/assign`, {
    method: "PATCH",
    body: payload,
  });
}

export async function updateLeadStage(leadId, payload) {
  return request(`/leads/${encodeURIComponent(leadId)}/stage`, {
    method: "PATCH",
    body: payload,
  });
}

export async function archiveLead(leadId, payload) {
  return request(`/leads/${encodeURIComponent(leadId)}/archive`, {
    method: "PATCH",
    body: payload,
  });
}

export async function restoreLead(leadId, payload) {
  return request(`/leads/${encodeURIComponent(leadId)}/restore`, {
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

export async function rescheduleLeadFollowUp(leadId, followUpId, payload) {
  return request(`/leads/${encodeURIComponent(leadId)}/follow-ups/${encodeURIComponent(followUpId)}/reschedule`, {
    method: "PATCH",
    body: payload,
  });
}

export async function cancelLeadFollowUp(leadId, followUpId, payload) {
  return request(`/leads/${encodeURIComponent(leadId)}/follow-ups/${encodeURIComponent(followUpId)}/cancel`, {
    method: "PATCH",
    body: payload,
  });
}

export async function completeLeadFollowUp(leadId, followUpId, payload) {
  return request(`/leads/${encodeURIComponent(leadId)}/follow-ups/${encodeURIComponent(followUpId)}/complete`, {
    method: "POST",
    body: payload,
  });
}

function normalizeLeadList(response) {
  if (Array.isArray(response)) {
    return {
      items: response,
      page: 1,
      pageSize: response.length,
      total: response.length,
    };
  }

  return {
    items: response?.items || [],
    page: response?.page || 1,
    pageSize: response?.pageSize || 25,
    total: response?.total || 0,
  };
}
