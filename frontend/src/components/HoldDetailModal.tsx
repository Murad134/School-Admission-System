import React, { useState, useEffect, useCallback } from 'react';
import { api } from '../services/api';
import { HoldStatus } from '../types';
import { useStudent } from '../context/StudentContext';
import { NotificationMessage } from './NotificationBanner';

interface HoldDetailModalProps {
  holdId: string | null;
  onClose: () => void;
  onStatusChanged: () => void;
  setNotification: (notif: NotificationMessage | null) => void;
}

export const HoldDetailModal: React.FC<HoldDetailModalProps> = ({
  holdId,
  onClose,
  onStatusChanged,
  setNotification
}) => {
  const { studentId } = useStudent();
  const [loading, setLoading] = useState(true);
  const [actionLoading, setActionLoading] = useState(false);
  const [classId, setClassId] = useState('');
  const [status, setStatus] = useState<HoldStatus>('HELD');
  const [expiresAt, setExpiresAt] = useState<string>('');
  const [remainingSeconds, setRemainingSeconds] = useState<number>(0);

  const fetchHold = useCallback(async () => {
    if (!holdId) return;
    try {
      setLoading(true);
      const data = await api.getHoldById(holdId);
      setClassId(data.classId);
      setStatus(data.status);
      setExpiresAt(data.expiresAt);

      const diffSec = Math.max(0, Math.floor((new Date(data.expiresAt).getTime() - Date.now()) / 1000));
      setRemainingSeconds(diffSec);

      if (diffSec <= 0 && data.status === 'HELD') {
        setStatus('EXPIRED');
      }
    } catch (err: unknown) {
      const error = err as { detail?: string };
      setNotification({
        type: 'error',
        text: error.detail || 'আবেদনের তথ্য লোড করা যায়নি।'
      });
      onClose();
    } finally {
      setLoading(false);
    }
  }, [holdId, onClose, setNotification]);

  useEffect(() => {
    fetchHold();
  }, [fetchHold]);

  // Live countdown timer: updates every second without full page reload
  useEffect(() => {
    if (!expiresAt || status !== 'HELD') return;

    const interval = setInterval(() => {
      const remaining = Math.max(0, Math.floor((new Date(expiresAt).getTime() - Date.now()) / 1000));
      setRemainingSeconds(remaining);

      // Auto-transition to EXPIRED without page reload
      if (remaining <= 0) {
        setStatus('EXPIRED');
        clearInterval(interval);
      }
    }, 1000);

    return () => clearInterval(interval);
  }, [expiresAt, status]);

  const handleConfirm = async () => {
    if (!holdId || !studentId) return;

    try {
      setActionLoading(true);
      const res = await api.confirmHold(holdId, studentId);
      setStatus(res.status);
      setNotification({
        type: 'success',
        text: 'ভর্তি ফি সফলভাবে জমা হয়েছে এবং আপনার ভর্তি নিশ্চিত (CONFIRMED) হয়েছে!'
      });
      onStatusChanged();
    } catch (err: unknown) {
      const error = err as { detail?: string };
      setNotification({
        type: 'error',
        text: error.detail || 'ভর্তি নিশ্চিত করতে সমস্যা হয়েছে।'
      });
    } finally {
      setActionLoading(false);
    }
  };

  const handleCancel = async () => {
    if (!holdId || !studentId) return;

    if (!window.confirm('আপনি কি নিশ্চিত যে আবেদনটি বাতিল করতে চান? আসনটি আবার উন্মুক্ত হয়ে যাবে।')) {
      return;
    }

    try {
      setActionLoading(true);
      await api.cancelHold(holdId, studentId);
      setStatus('CANCELLED');
      setNotification({
        type: 'info',
        text: 'আবেদন বাতিল করা হয়েছে এবং আসনটি উন্মুক্ত হয়েছে।'
      });
      onStatusChanged();
    } catch (err: unknown) {
      const error = err as { detail?: string };
      setNotification({
        type: 'error',
        text: error.detail || 'আবেদন বাতিল করতে সমস্যা হয়েছে।'
      });
    } finally {
      setActionLoading(false);
    }
  };

  if (!holdId) return null;

  const minutes = Math.floor(remainingSeconds / 60);
  const seconds = remainingSeconds % 60;
  const formattedTime = `${String(minutes).padStart(2, '0')}:${String(seconds).padStart(2, '0')}`;

  const isExpiredOrCancelled = status === 'EXPIRED' || status === 'CANCELLED';
  const isConfirmed = status === 'CONFIRMED';
  const isHeldActive = status === 'HELD' && remainingSeconds > 0;

  return (
    <div className="modal-backdrop">
      <div className="modal-card hold-detail-card">
        <div className="modal-header">
          <h3>আবেদনের বিস্তারিত ও স্থিতি (Application Details)</h3>
          <button className="close-btn" onClick={onClose} disabled={actionLoading}>
            &times;
          </button>
        </div>

        {loading ? (
          <div className="loading-spinner">তথ্য লোড হচ্ছে...</div>
        ) : (
          <div className="hold-body">
            <div className="hold-info-grid">
              <div className="info-item">
                <span className="label">আবেদন আইডি (Hold ID):</span>
                <span className="value code">{holdId}</span>
              </div>
              <div className="info-item">
                <span className="label">শ্রেণি আইডি (Class ID):</span>
                <span className="value code">{classId}</span>
              </div>
              <div className="info-item">
                <span className="label">শিক্ষার্থীর জন্ম নিবন্ধন:</span>
                <span className="value">{studentId}</span>
              </div>
              <div className="info-item">
                <span className="label">বর্তমান অবস্থা (Status):</span>
                <span className={`status-badge status-${status.toLowerCase()}`}>
                  {status === 'HELD' && '⏳ HELD (আসন সাময়িক সংরক্ষিত)'}
                  {status === 'CONFIRMED' && '✓ CONFIRMED (ভর্তি নিশ্চিত)'}
                  {status === 'EXPIRED' && '✕ EXPIRED (মেয়াদ উত্তীর্ণ)'}
                  {status === 'CANCELLED' && '⊘ CANCELLED (বাতিলকৃত)'}
                </span>
              </div>
            </div>

            {/* Countdown timer section */}
            {isHeldActive && (
              <div className="timer-box">
                <div className="timer-label">ফি জমা দিয়ে ভর্তি নিশ্চিত করার সময় বাকি:</div>
                <div className="timer-countdown">{formattedTime}</div>
                <div className="timer-hint">
                  নির্ধারিত সময়ের মধ্যে ফি জমা না দিলে আসনটি নিজে থেকে পুনরায় সবার জন্য উন্মুক্ত হবে।
                </div>
              </div>
            )}

            {status === 'EXPIRED' && (
              <div className="alert-box alert-expired">
                ⚠ আবেদনের নির্ধারিত মেয়াদ শেষ হয়েছে। আসনটি পুনরায় উন্মুক্ত হয়ে গেছে।
              </div>
            )}

            {isConfirmed && (
              <div className="alert-box alert-confirmed">
                🎉 অভিনন্দন! এই শ্রেণিতে শিক্ষার্থীর ভর্তি সম্পূর্ণ নিশ্চিত হয়েছে।
              </div>
            )}

            {status === 'CANCELLED' && (
              <div className="alert-box alert-cancelled">
                এই আবেদনটি বাতিল করা হয়েছে।
              </div>
            )}

            <div className="modal-actions">
              <button
                type="button"
                className="btn-danger"
                onClick={handleCancel}
                disabled={actionLoading || isConfirmed || isExpiredOrCancelled}
              >
                {actionLoading ? 'অপেক্ষা করুন...' : 'বাতিল (Cancel)'}
              </button>

              <button
                type="button"
                className="btn-success"
                onClick={handleConfirm}
                disabled={actionLoading || !isHeldActive}
              >
                {actionLoading ? 'যাচাই হচ্ছে...' : 'ভর্তি ফি দিন (Confirm Admission)'}
              </button>

              <button type="button" className="btn-secondary" onClick={onClose} disabled={actionLoading}>
                বন্ধ করুন
              </button>
            </div>
          </div>
        )}
      </div>
    </div>
  );
};

