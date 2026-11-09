import React, { useState, useEffect, useCallback } from 'react';
import { api } from '../services/api';
import { SchoolClass } from '../types';
import { useStudent } from '../context/StudentContext';
import { NotificationMessage } from './NotificationBanner';

interface ClassListProps {
  onHoldCreated: (holdId: string) => void;
  setNotification: (notif: NotificationMessage | null) => void;
}

export const ClassList: React.FC<ClassListProps> = ({ onHoldCreated, setNotification }) => {
  const { studentId, isValidStudentId, setActiveHoldId } = useStudent();
  const [classes, setClasses] = useState<SchoolClass[]>([]);
  const [loading, setLoading] = useState(true);
  const [applyingClassId, setApplyingClassId] = useState<string | null>(null);

  const fetchClasses = useCallback(async (isInitial = false) => {
    try {
      if (isInitial) setLoading(true);
      const data = await api.getClasses();
      setClasses(data);
    } catch (err: unknown) {
      if (isInitial) {
        const error = err as { detail?: string };
        setNotification({
          type: 'error',
          text: error.detail || 'ক্লাসের তালিকা লোড করতে ব্যর্থ হয়েছে। সার্ভার চালু আছে কি না পরীক্ষা করুন।'
        });
      }
    } finally {
      if (isInitial) setLoading(false);
    }
  }, [setNotification]);

  // Initial load
  useEffect(() => {
    fetchClasses(true);
  }, [fetchClasses]);

  // Real-time polling: Refresh seat counts every 3.5 seconds without page reload
  useEffect(() => {
    const interval = setInterval(() => {
      fetchClasses(false);
    }, 3500);

    return () => clearInterval(interval);
  }, [fetchClasses]);

  const handleApply = async (schoolClass: SchoolClass) => {
    if (!isValidStudentId) {
      setNotification({
        type: 'warning',
        text: 'আবেদন করতে উপরে শিক্ষার্থীর ১৭ অঙ্কের সঠিক জন্ম নিবন্ধন নম্বর লিখুন।'
      });
      const inputEl = document.getElementById('student-id-input');
      inputEl?.focus();
      return;
    }

    if (schoolClass.availableSeats <= 0) {
      setNotification({
        type: 'error',
        text: `দুঃখিত, "${schoolClass.name}"-এ কোনো খালি আসন নেই (আসন পূর্ণ)।`
      });
      return;
    }

    try {
      setApplyingClassId(schoolClass.id);
      const hold = await api.createHold(schoolClass.id, studentId);

      setActiveHoldId(hold.holdId);
      setNotification({
        type: 'success',
        text: `"${schoolClass.name}"-এ আপনার জন্য ১টি আসন সাময়িকভাবে সংরক্ষিত (HELD) হয়েছে! নির্ধারিত সময়ের মধ্যে ফি দিন।`
      });

      // Refresh seat counters immediately
      fetchClasses(false);

      // Open the hold modal
      onHoldCreated(hold.holdId);
    } catch (err: unknown) {
      const error = err as { detail?: string; status?: number };
      setNotification({
        type: 'error',
        text: error.detail || 'আবেদন সম্পন্ন করা যায়নি।'
      });
    } finally {
      setApplyingClassId(null);
    }
  };

  if (loading) {
    return <div className="loading-state">ক্লাস ও আসনের তথ্য লোড হচ্ছে...</div>;
  }

  if (classes.length === 0) {
    return (
      <div className="empty-state">
        <h3>কোনো শ্রেণি বা শিফট পাওয়া যায়নি</h3>
        <p>এখনো কোনো ক্লাস তৈরি করা হয়নি। উপরের "নতুন ক্লাস তৈরি (Admin)" বাটনে ক্লিক করে ক্লাস যোগ করুন।</p>
      </div>
    );
  }

  return (
    <div className="class-list-container">
      <div className="section-header">
        <div>
          <h2>ভর্তির জন্য উন্মুক্ত শ্রেণি ও শিফটসমূহ</h2>
          <p className="section-subtitle">
            আগে আসলে আগে পাবেন ভিত্তিতে আসন বরাদ্দ। তথ্য ৩.৫ সেকেন্ড পরপর নিজে থেকেই হালনাগাদ হচ্ছে।
          </p>
        </div>
        <button className="refresh-btn" onClick={() => fetchClasses(false)} title="হালনাগাদ করুন">
          🔄 রিফ্রেশ
        </button>
      </div>

      <div className="class-grid">
        {classes.map((cls) => {
          const isFull = cls.availableSeats <= 0;
          const isApplying = applyingClassId === cls.id;
          const occupied = cls.totalSeats - cls.availableSeats;
          const occupiedPercent = Math.min(100, Math.round((occupied / cls.totalSeats) * 100));

          return (
            <div key={cls.id} className={`class-card ${isFull ? 'class-full' : ''}`}>
              <div className="card-top">
                <span className="class-id-pill">{cls.id}</span>
                <span className={`seat-badge ${isFull ? 'badge-full' : 'badge-available'}`}>
                  {isFull ? 'আসন পূর্ণ' : `${cls.availableSeats}টি আসন খালি`}
                </span>
              </div>

              <h3 className="class-name">{cls.name}</h3>

              <div className="seat-meter">
                <div
                  className={`seat-meter-fill ${isFull ? 'meter-danger' : 'meter-normal'}`}
                  style={{ width: `${occupiedPercent}%` }}
                />
              </div>

              <div className="seat-stats-grid">
                <div className="stat-box">
                  <span className="stat-label">মোট আসন</span>
                  <span className="stat-num">{cls.totalSeats}</span>
                </div>
                <div className="stat-box">
                  <span className="stat-label">হোল্ডে আছে</span>
                  <span className="stat-num stat-held">{cls.heldSeats}</span>
                </div>
                <div className="stat-box">
                  <span className="stat-label">নিশ্চিত</span>
                  <span className="stat-num stat-confirmed">{cls.confirmedSeats}</span>
                </div>
                <div className="stat-box">
                  <span className="stat-label">খালি আছে</span>
                  <span className="stat-num stat-avail">{cls.availableSeats}</span>
                </div>
              </div>

              <button
                className={`btn-apply ${isFull ? 'btn-disabled' : ''}`}
                onClick={() => handleApply(cls)}
                disabled={isFull || isApplying}
              >
                {isApplying
                  ? 'আবেদন প্রক্রিয়াধীন...'
                  : isFull
                  ? 'আসন পূর্ণ (Full)'
                  : 'আবেদন করুন (Apply Now)'}
              </button>
            </div>
          );
        })}
      </div>
    </div>
  );
};
