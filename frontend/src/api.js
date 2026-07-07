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

export function updateStoredUser(user) {
  if (user) {
    localStorage.setItem(USER_STORAGE_KEY, JSON.stringify(user));
  }
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

async function requestForm(path, formData, method = "POST") {
  const { token } = getStoredAuth();
  const headers = {};
  if (token) {
    headers.Authorization = `Bearer ${token}`;
  }

  const response = await fetch(`${API_BASE_URL}${path}`, {
    method,
    headers,
    body: formData,
  });

  if (!response.ok) {
    throw await createApiError(response);
  }

  return response.json();
}

async function requestBlob(path) {
  const { token } = getStoredAuth();
  const headers = {};
  if (token) {
    headers.Authorization = `Bearer ${token}`;
  }

  const response = await fetch(`${API_BASE_URL}${path}`, {
    method: "GET",
    headers,
  });

  if (!response.ok) {
    throw await createApiError(response);
  }

  const blob = await response.blob();
  const disposition = response.headers.get("content-disposition") || "";
  return {
    blob,
    filename: getFilenameFromDisposition(disposition),
  };
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

export async function getNotifications(params = {}) {
  const query = new URLSearchParams();
  Object.entries(params).forEach(([key, value]) => {
    if (value !== undefined && value !== null && value !== "") {
      query.set(key, value);
    }
  });
  const suffix = query.toString() ? `?${query.toString()}` : "";
  return request(`/notifications${suffix}`);
}

export async function getNotificationUnreadCount() {
  return request("/notifications/unread-count");
}

export async function markNotificationRead(notificationId) {
  return request(`/notifications/${encodeURIComponent(notificationId)}/read`, { method: "POST" });
}

export async function markAllNotificationsRead() {
  return request("/notifications/read-all", { method: "POST" });
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
    error.data = body;
    return error;
  } catch {
    const error = new Error(`Request failed with status ${response.status}`);
    error.status = response.status;
    error.errors = {};
    return error;
  }
}

export async function getCrmData() {
  const [dashboard, advancedDashboard, leads, pipeline, followUps, leadOptions, communicationTemplates] = await Promise.all([
    request("/dashboard"),
    request("/dashboard/advanced"),
    request("/leads"),
    request("/pipeline"),
    request("/follow-ups"),
    request("/leads/options"),
    request("/communication-templates"),
  ]);

  return {
    dashboard,
    advancedDashboard,
    leads: normalizeLeadList(leads),
    pipeline,
    followUps,
    leadOptions,
    communicationTemplates,
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

export async function previewLeadImport(payload) {
  return requestForm("/leads/import/preview", createLeadImportFormData(payload));
}

export async function commitLeadImport(payload) {
  return requestForm("/leads/import/commit", createLeadImportFormData(payload));
}

export async function downloadLeadImportTemplate(format = "xlsx") {
  return requestBlob(`/leads/import/template?format=${encodeURIComponent(format)}`);
}

export async function exportLeads(params = {}, format = "xlsx") {
  const query = new URLSearchParams();
  Object.entries({ ...params, format }).forEach(([key, value]) => {
    if (value !== undefined && value !== null && value !== "") {
      query.set(key, value);
    }
  });

  return requestBlob(`/leads/export${query.toString() ? `?${query}` : ""}`);
}

export async function getReports(params = {}) {
  const query = new URLSearchParams();
  Object.entries(params).forEach(([key, value]) => {
    if (value !== undefined && value !== null && value !== "") {
      query.set(key, value);
    }
  });

  return request(`/reports${query.toString() ? `?${query}` : ""}`);
}

export async function getCounsellorWorkInsights(params = {}) {
  const query = new URLSearchParams();
  ["startDate", "endDate", "courseId", "sourceId"].forEach((key) => {
    const value = params[key];
    if (value !== undefined && value !== null && value !== "") query.set(key, value);
  });
  return request(`/reports/counsellor-workspace${query.toString() ? `?${query}` : ""}`);
}

export async function exportReports(params = {}, format = "xlsx") {
  const query = new URLSearchParams();
  Object.entries({ ...params, format }).forEach(([key, value]) => {
    if (value !== undefined && value !== null && value !== "") {
      query.set(key, value);
    }
  });

  return requestBlob(`/reports/export${query.toString() ? `?${query}` : ""}`);
}

export async function getApplications(params = {}) {
  const query = new URLSearchParams();
  Object.entries(params).forEach(([key, value]) => {
    if (value !== undefined && value !== null && value !== "") query.set(key, value);
  });
  return request(`/applications${query.toString() ? `?${query}` : ""}`);
}

export async function getApplicationDetail(applicationId) {
  return request(`/applications/${encodeURIComponent(applicationId)}`);
}

export async function createLeadApplication(leadId, payload = {}) {
  return request(`/leads/${encodeURIComponent(leadId)}/applications`, {
    method: "POST",
    body: payload,
  });
}

export async function transitionApplication(applicationId, payload) {
  return request(`/applications/${encodeURIComponent(applicationId)}/transitions`, {
    method: "POST",
    body: payload,
  });
}

export async function updateApplicationChecklistItem(applicationId, itemId, payload) {
  return request(`/applications/${encodeURIComponent(applicationId)}/checklist/${encodeURIComponent(itemId)}`, {
    method: "PATCH",
    body: payload,
  });
}

export async function enrollApplication(applicationId, payload) {
  return request(`/applications/${encodeURIComponent(applicationId)}/enroll`, {
    method: "POST",
    body: payload,
  });
}

export async function getEnrollments(params = {}) {
  const query = new URLSearchParams();
  Object.entries(params).forEach(([key, value]) => {
    if (value !== undefined && value !== null && value !== "") query.set(key, value);
  });
  return request(`/enrollments${query.toString() ? `?${query}` : ""}`);
}

export async function getEnrollmentDetail(enrollmentId) {
  return request(`/enrollments/${encodeURIComponent(enrollmentId)}`);
}

export async function updateEnrollmentStatus(enrollmentId, payload) {
  return request(`/enrollments/${encodeURIComponent(enrollmentId)}/status`, {
    method: "PATCH",
    body: payload,
  });
}

export async function getPlatformTenants() {
  return request("/platform/tenants");
}

export async function getCurrentTenant() {
  return request("/tenants/current");
}

export async function updateCurrentTenant(payload) {
  return request("/tenants/current", {
    method: "PATCH",
    body: payload,
  });
}

export async function getLeadIntelligenceSettings() {
  return request("/lead-intelligence/settings");
}

export async function updateLeadIntelligenceSettings(payload) {
  return request("/lead-intelligence/settings", {
    method: "PATCH",
    body: payload,
  });
}

export async function createLeadDistributionRule(payload) {
  return request("/lead-intelligence/rules", {
    method: "POST",
    body: payload,
  });
}

export async function updateLeadDistributionRule(ruleId, payload) {
  return request(`/lead-intelligence/rules/${encodeURIComponent(ruleId)}`, {
    method: "PATCH",
    body: payload,
  });
}

export async function recalculateLeadIntelligence() {
  return request("/lead-intelligence/recalculate", {
    method: "POST",
  });
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

export async function getCommunicationTemplates(params = {}) {
  const query = new URLSearchParams();
  Object.entries(params).forEach(([key, value]) => {
    if (value !== undefined && value !== null && value !== "") {
      query.set(key, value);
    }
  });

  return request(`/communication-templates${query.toString() ? `?${query}` : ""}`);
}

export async function createCommunicationTemplate(payload) {
  return request("/communication-templates", {
    method: "POST",
    body: payload,
  });
}

export async function updateCommunicationTemplate(templateId, payload) {
  return request(`/communication-templates/${encodeURIComponent(templateId)}`, {
    method: "PATCH",
    body: payload,
  });
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
  const [lead, documents, payments] = await Promise.all([
    request(`/leads/${encodeURIComponent(leadId)}`),
    getLeadDocuments(leadId),
    getLeadPayments(leadId),
  ]);

  return {
    ...lead,
    documents: documents.items || [],
    payments: payments.items || [],
  };
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

export async function runBulkLeadAction(payload) {
  return request("/leads/bulk-actions", {
    method: "POST",
    body: payload,
  });
}

export async function addLeadActivity(leadId, payload) {
  return request(`/leads/${encodeURIComponent(leadId)}/activities`, {
    method: "POST",
    body: payload,
  });
}

export async function applyCommunicationTemplate(leadId, payload) {
  return request(`/leads/${encodeURIComponent(leadId)}/template-activities`, {
    method: "POST",
    body: payload,
  });
}

export async function getLeadDocuments(leadId) {
  return request(`/leads/${encodeURIComponent(leadId)}/documents`);
}

export async function uploadLeadDocument(leadId, payload) {
  const formData = new FormData();
  formData.append("documentTypeId", payload.documentTypeId);
  formData.append("file", payload.file);
  if (payload.version) {
    formData.append("version", String(payload.version));
  }
  if (payload.notes) {
    formData.append("notes", payload.notes);
  }

  return requestForm(`/leads/${encodeURIComponent(leadId)}/documents`, formData);
}

export async function verifyLeadDocument(leadId, documentId, payload) {
  return request(`/leads/${encodeURIComponent(leadId)}/documents/${encodeURIComponent(documentId)}/verify`, {
    method: "PATCH",
    body: payload,
  });
}

export async function rejectLeadDocument(leadId, documentId, payload) {
  return request(`/leads/${encodeURIComponent(leadId)}/documents/${encodeURIComponent(documentId)}/reject`, {
    method: "PATCH",
    body: payload,
  });
}

export async function deleteLeadDocument(leadId, documentId, payload) {
  return request(`/leads/${encodeURIComponent(leadId)}/documents/${encodeURIComponent(documentId)}`, {
    method: "DELETE",
    body: payload,
  });
}

export async function downloadLeadDocument(leadId, documentId) {
  return requestBlob(`/leads/${encodeURIComponent(leadId)}/documents/${encodeURIComponent(documentId)}/download`);
}

export async function getLeadPayments(leadId) {
  return request(`/leads/${encodeURIComponent(leadId)}/payments`);
}

export async function createLeadPayment(leadId, payload) {
  return request(`/leads/${encodeURIComponent(leadId)}/payments`, {
    method: "POST",
    body: payload,
  });
}

export async function updateLeadPayment(leadId, paymentId, payload) {
  return request(`/leads/${encodeURIComponent(leadId)}/payments/${encodeURIComponent(paymentId)}`, {
    method: "PATCH",
    body: payload,
  });
}

export async function addLeadPaymentTransaction(leadId, paymentId, payload) {
  return request(`/leads/${encodeURIComponent(leadId)}/payments/${encodeURIComponent(paymentId)}/transactions`, {
    method: "POST",
    body: payload,
  });
}

export async function cancelLeadPayment(leadId, paymentId, payload) {
  return request(`/leads/${encodeURIComponent(leadId)}/payments/${encodeURIComponent(paymentId)}/cancel`, {
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

function createLeadImportFormData({ file, mapping, duplicateMode, fingerprint }) {
  const formData = new FormData();
  formData.append("file", file);
  formData.append("duplicateMode", duplicateMode || "skip");
  if (mapping) {
    formData.append("mapping", JSON.stringify(mapping));
  }
  if (fingerprint) {
    formData.append("fingerprint", fingerprint);
  }
  return formData;
}

function getFilenameFromDisposition(disposition) {
  const utfMatch = disposition.match(/filename\*=UTF-8''([^;]+)/i);
  if (utfMatch) {
    return decodeURIComponent(utfMatch[1].replace(/"/g, ""));
  }

  const match = disposition.match(/filename="?([^";]+)"?/i);
  return match ? match[1] : "";
}
