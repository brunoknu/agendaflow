/**
 * API client for AgendaFlow.
 * - Cookies sent automatically (credentials: 'include')
 * - CSRF token fetched once and attached to mutating requests
 * - 401 triggers redirect to login
 * - Error responses use Problem Details (RFC 7807)
 */

let csrfToken: string | null = null;

async function getCsrfToken(): Promise<string> {
  if (csrfToken) return csrfToken;
  const res = await fetch('/api/antiforgery/token', { credentials: 'include' });
  if (!res.ok) throw new Error('Failed to fetch CSRF token');
  const data = (await res.json()) as { token: string };
  csrfToken = data.token;
  return csrfToken;
}

function invalidateCsrfToken() {
  csrfToken = null;
}

type HttpMethod = 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE';

export class ApiError extends Error {
  readonly status: number;
  readonly title: string;
  readonly detail?: string;
  readonly errors?: Record<string, string[]>;

  constructor(status: number, title: string, detail?: string, errors?: Record<string, string[]>) {
    super(detail ?? title);
    this.name = 'ApiError';
    this.status = status;
    this.title = title;
    this.detail = detail;
    this.errors = errors;
  }
}

async function request<T>(method: HttpMethod, path: string, body?: unknown): Promise<T> {
  const headers: Record<string, string> = { 'Content-Type': 'application/json' };

  if (method !== 'GET') {
    try {
      headers['X-XSRF-TOKEN'] = await getCsrfToken();
    } catch {
      // unauthenticated requests don't need CSRF
    }
  }

  const res = await fetch(path, {
    method,
    credentials: 'include',
    headers,
    body: body !== undefined ? JSON.stringify(body) : undefined,
  });

  if (res.status === 401) {
    invalidateCsrfToken();
    window.location.href = '/login';
    throw new ApiError(401, 'Unauthorized', 'Your session has expired.');
  }

  if (res.status === 204) return undefined as T;

  const json = (await res.json().catch(() => null)) as Record<string, unknown> | null;

  if (!res.ok) {
    throw new ApiError(
      res.status,
      typeof json?.['title'] === 'string' ? json['title'] : 'Error',
      typeof json?.['detail'] === 'string' ? json['detail'] : undefined,
      json?.['errors'] as Record<string, string[]> | undefined,
    );
  }

  return json as T;
}

export const api = {
  get: <T>(path: string) => request<T>('GET', path),
  post: <T>(path: string, body?: unknown) => request<T>('POST', path, body),
  put: <T>(path: string, body?: unknown) => request<T>('PUT', path, body),
  patch: <T>(path: string, body?: unknown) => request<T>('PATCH', path, body),
  delete: <T>(path: string) => request<T>('DELETE', path),
};

// ── Auth ─────────────────────────────────────────────────────────

export interface UserInfo {
  userId: string;
  email: string;
  fullName: string;
  tenants: { tenantId: string; tenantName: string; role: string }[];
}

export const authApi = {
  register: (data: { email: string; password: string; fullName: string }) =>
    api.post<{ message: string }>('/api/auth/register', data),

  login: (data: { email: string; password: string }) =>
    api.post<UserInfo>('/api/auth/login', data),

  logout: () => api.post<void>('/api/auth/logout'),

  me: () => api.get<UserInfo>('/api/auth/me'),

  forgotPassword: (email: string) =>
    api.post<{ message: string }>('/api/auth/forgot-password', { email }),

  resetPassword: (data: { email: string; token: string; newPassword: string }) =>
    api.post<{ message: string }>('/api/auth/reset-password', data),
};

// ── Services ──────────────────────────────────────────────────────

export interface ServiceDto {
  id: string;
  name: string;
  description?: string;
  durationMinutes: number;
  price: number;
  currency: string;
  bufferBeforeMinutes: number;
  bufferAfterMinutes: number;
  isActive: boolean;
  createdAtUtc: string;
}

export const servicesApi = {
  list: (active?: boolean) =>
    api.get<ServiceDto[]>(`/api/services${active !== undefined ? `?active=${String(active)}` : ''}`),

  get: (id: string) => api.get<ServiceDto>(`/api/services/${id}`),

  create: (data: Omit<ServiceDto, 'id' | 'isActive' | 'createdAtUtc'>) =>
    api.post<ServiceDto>('/api/services', data),

  update: (id: string, data: Omit<ServiceDto, 'id' | 'isActive' | 'createdAtUtc'>) =>
    api.put<ServiceDto>(`/api/services/${id}`, data),

  activate: (id: string) => api.patch<void>(`/api/services/${id}/activate`),
  deactivate: (id: string) => api.patch<void>(`/api/services/${id}/deactivate`),
};

// ── Professionals ─────────────────────────────────────────────────

export interface ProfessionalDto {
  id: string;
  name: string;
  email?: string;
  phone?: string;
  isActive: boolean;
  createdAtUtc: string;
  serviceIds?: string[];
}

export const professionalsApi = {
  list: (active?: boolean) =>
    api.get<ProfessionalDto[]>(`/api/professionals${active !== undefined ? `?active=${String(active)}` : ''}`),

  get: (id: string) => api.get<ProfessionalDto>(`/api/professionals/${id}`),

  create: (data: { name: string; email?: string; phone?: string }) =>
    api.post<ProfessionalDto>('/api/professionals', data),

  update: (id: string, data: { name: string; email?: string; phone?: string }) =>
    api.put<ProfessionalDto>(`/api/professionals/${id}`, data),

  activate: (id: string) => api.patch<void>(`/api/professionals/${id}/activate`),
  deactivate: (id: string) => api.patch<void>(`/api/professionals/${id}/deactivate`),

  linkService: (professionalId: string, serviceId: string) =>
    api.post<void>(`/api/professionals/${professionalId}/services/${serviceId}`),

  unlinkService: (professionalId: string, serviceId: string) =>
    api.delete<void>(`/api/professionals/${professionalId}/services/${serviceId}`),
};

// ── Appointments ──────────────────────────────────────────────────

export interface AppointmentDto {
  id: string;
  professionalId: string;
  professionalName: string;
  serviceId: string;
  serviceName: string;
  customerId: string;
  customerName: string;
  startAtUtc: string;
  endAtUtc: string;
  status: string;
  source: string;
  notes?: string;
  createdAtUtc: string;
}

export interface PaginatedResponse<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export const appointmentsApi = {
  list: (params?: Record<string, string>) => {
    const qs = params ? '?' + new URLSearchParams(params).toString() : '';
    return api.get<PaginatedResponse<AppointmentDto>>(`/api/appointments${qs}`);
  },

  get: (id: string) => api.get<AppointmentDto>(`/api/appointments/${id}`),

  updateStatus: (id: string, newStatus: string, reason?: string) =>
    api.patch<void>(`/api/appointments/${id}/status`, { newStatus, reason }),
};

// ── Public booking ────────────────────────────────────────────────

export interface PublicServiceDto {
  id: string;
  name: string;
  description?: string;
  durationMinutes: number;
  price: number;
}

export interface PublicProfessionalDto {
  id: string;
  name: string;
}

export const publicApi = {
  getServices: (tenantSlug: string) =>
    api.get<PublicServiceDto[]>(`/api/public/${tenantSlug}/services`),

  getProfessionals: (tenantSlug: string, serviceId?: string) =>
    api.get<PublicProfessionalDto[]>(
      `/api/public/${tenantSlug}/professionals${serviceId ? `?serviceId=${serviceId}` : ''}`
    ),

  getAvailableSlots: (tenantSlug: string, professionalId: string, serviceId: string, date: string) =>
    api.get<{ slots: string[] }>(
      `/api/public/${tenantSlug}/availability?professionalId=${professionalId}&serviceId=${serviceId}&date=${date}`
    ),

  book: (
    tenantSlug: string,
    data: {
      professionalId: string;
      serviceId: string;
      startAtUtc: string;
      customerName: string;
      customerEmail: string;
      customerPhone?: string;
      notes?: string;
      idempotencyKey: string;
    }
  ) => api.post<AppointmentDto>(`/api/public/${tenantSlug}/appointments`, data),

  confirmByToken: (token: string) =>
    api.post<{ message: string }>(`/api/public/bookings/confirm?token=${encodeURIComponent(token)}`),

  cancelByToken: (token: string) =>
    api.post<{ message: string }>(`/api/public/bookings/cancel?token=${encodeURIComponent(token)}`),
};

// ── Reports ───────────────────────────────────────────────────────

export interface AppointmentsReport {
  from: string;
  to: string;
  total: number;
  completed: number;
  cancelled: number;
  noShow: number;
  cancellationRate: number;
  byStatus: { status: string; count: number }[];
  byProfessional: { professionalName: string; count: number }[];
  byService: { serviceName: string; count: number }[];
  byDay: { date: string; count: number }[];
}

export const reportsApi = {
  appointments: (from?: string, to?: string) => {
    const params = new URLSearchParams();
    if (from) params.set('from', from);
    if (to) params.set('to', to);
    const qs = params.toString();
    return api.get<AppointmentsReport>(`/api/reports/appointments${qs ? `?${qs}` : ''}`);
  },
};
