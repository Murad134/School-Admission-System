import {
  SchoolClass,
  CreateClassPayload,
  HoldResponse,
  HoldDetail,
  ConfirmHoldResponse,
  StudentHold,
  ApiError
} from '../types';

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || 'http://localhost:8080';

async function handleResponse<T>(response: Response): Promise<T> {
  if (response.status === 204) {
    return {} as T;
  }

  const contentType = response.headers.get('content-type');
  const isJson = contentType && contentType.includes('application/json');

  if (!response.ok) {
    let errorDetail = 'সার্ভার থেকে ত্রুটি এসেছে। অনুগ্রহ করে পুনরায় চেষ্টা করুন।';
    if (isJson) {
      try {
        const errorJson = await response.json();
        errorDetail = errorJson.detail || errorJson.title || errorDetail;
      } catch {
        // Fallback to text
      }
    } else {
      const text = await response.text();
      if (text) errorDetail = text;
    }

    const error: ApiError = {
      status: response.status,
      title: response.statusText,
      detail: errorDetail
    };
    throw error;
  }

  return (await response.json()) as T;
}

export const api = {
  async getClasses(): Promise<SchoolClass[]> {
    const res = await fetch(`${API_BASE_URL}/classes`, {
      headers: { Accept: 'application/json' }
    });
    return handleResponse<SchoolClass[]>(res);
  },

  async getClassById(classId: string): Promise<SchoolClass> {
    const res = await fetch(`${API_BASE_URL}/classes/${encodeURIComponent(classId)}`, {
      headers: { Accept: 'application/json' }
    });
    return handleResponse<SchoolClass>(res);
  },

  async createClass(payload: CreateClassPayload): Promise<{ id: string; name: string; totalSeats: number }> {
    const res = await fetch(`${API_BASE_URL}/classes`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        Accept: 'application/json'
      },
      body: JSON.stringify(payload)
    });
    return handleResponse<{ id: string; name: string; totalSeats: number }>(res);
  },

  async createHold(classId: string, studentId: string): Promise<HoldResponse> {
    const res = await fetch(`${API_BASE_URL}/classes/${encodeURIComponent(classId)}/holds`, {
      method: 'POST',
      headers: {
        'X-Student-Id': studentId.trim(),
        Accept: 'application/json'
      }
    });
    return handleResponse<HoldResponse>(res);
  },

  async getHoldById(holdId: string): Promise<HoldDetail> {
    const res = await fetch(`${API_BASE_URL}/holds/${encodeURIComponent(holdId)}`, {
      headers: { Accept: 'application/json' }
    });
    return handleResponse<HoldDetail>(res);
  },

  async confirmHold(holdId: string, studentId: string): Promise<ConfirmHoldResponse> {
    const res = await fetch(`${API_BASE_URL}/holds/${encodeURIComponent(holdId)}/confirm`, {
      method: 'POST',
      headers: {
        'X-Student-Id': studentId.trim(),
        Accept: 'application/json'
      }
    });
    return handleResponse<ConfirmHoldResponse>(res);
  },

  async cancelHold(holdId: string, studentId: string): Promise<void> {
    const res = await fetch(`${API_BASE_URL}/holds/${encodeURIComponent(holdId)}`, {
      method: 'DELETE',
      headers: {
        'X-Student-Id': studentId.trim()
      }
    });
    return handleResponse<void>(res);
  },

  async getMyHolds(studentId: string): Promise<StudentHold[]> {
    const res = await fetch(`${API_BASE_URL}/me/holds`, {
      headers: {
        'X-Student-Id': studentId.trim(),
        Accept: 'application/json'
      }
    });
    return handleResponse<StudentHold[]>(res);
  }
};

