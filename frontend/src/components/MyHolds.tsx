import React, { useState, useEffect, useCallback } from 'react';
import { api } from '../services/api';
import { StudentHold } from '../types';
import { useStudent } from '../context/StudentContext';
import { NotificationMessage } from './NotificationBanner';

interface MyHoldsProps {
  onSelectHold: (holdId: string) => void;
  setNotification: (notif: NotificationMessage | null) => void;
}

export const MyHolds: React.FC<MyHoldsProps> = ({ onSelectHold, setNotification }) => {
  const { studentId, isValidStudentId } = useStudent();
  const [holds, setHolds] = useState<StudentHold[]>([]);
  const [loading, setLoading] = useState(false);

  const fetchMyHolds = useCallback(async () => {
    if (!isValidStudentId) {
      setHolds([]);
      return;
    }

    try {
      setLoading(true);
      const data = await api.getMyHolds(studentId);
      setHolds(data);
    } catch (err: unknown) {
      const error = err as { detail?: string };
      setNotification({
        type: 'error',
        text: error.detail || 'আপনার আবেদন তালিকা লোড করা যায়নি।'
      });
    } finally {
      setLoading(false);
    }
  }, [studentId, isValidStudentId, setNotification]);

  useEffect(() => {
    fetchMyHolds();
  }, [fetchMyHolds]);

  if (!isValidStudentId) {
    return (
      <div className="empty-state">
        <h3>শিক্ষার্থীর জন্ম নিবন্ধন নম্বর দিন</h3>
        <p>আপনার সকল আবেদন দেখতে পেজের উপরে ১৭ অঙ্কের সঠিক জন্ম নিবন্ধন নম্বর লিখুন।</p>
      </div>
    );
  }

  if (loading) {
    return <div className="loading-state">আপনার আবেদনসমূহ লোড হচ্ছে...</div>;
  }

  if (holds.length === 0) {
    return (
      <div className="empty-state">
        <h3>এই জন্ম নিবন্ধন নম্বরে কোনো আবেদন পাওয়া যায়নি</h3>
        <p>
          শিক্ষার্থী ({studentId})-এর নামে এখনো কোনো আবেদন করা হয়নি। "ভর্তির শ্রেণি ও শিফট তালিকা" ট্যাব থেকে পছন্দের
          শ্রেণিতে আবেদন করুন।
        </p>
      </div>
    );
  }

  return (
    <div className="my-holds-container">
      <div className="section-header">
        <div>
          <h2>আমার সকল আবেদনসমূহ (My Applications)</h2>
          <p className="section-subtitle">শিক্ষার্থীর জন্ম নিবন্ধন: {studentId}</p>
        </div>
        <button className="refresh-btn" onClick={fetchMyHolds} title="হালনাগাদ করুন">
          🔄 রিফ্রেশ
        </button>
      </div>

      <div className="holds-table-wrapper">
        <table className="holds-table">
          <thead>
            <tr>
              <th>শ্রেণি ও শিফট</th>
              <th>আবেদন আইডি</th>
              <th>অবস্থা (Status)</th>
              <th>মেয়াদের সময় (UTC)</th>
              <th>পদক্ষেপ</th>
            </tr>
          </thead>
          <tbody>
            {holds.map((h) => {
              const isHeld = h.status === 'HELD';
              const isConfirmed = h.status === 'CONFIRMED';
              const isExpired = h.status === 'EXPIRED';
              const isCancelled = h.status === 'CANCELLED';

              return (
                <tr key={h.holdId}>
                  <td className="bold">{h.className || h.classId}</td>
                  <td>
                    <code className="code-pill">{h.holdId}</code>
                  </td>
                  <td>
                    <span className={`status-badge status-${h.status.toLowerCase()}`}>
                      {isHeld && '⏳ HELD (চলমান)'}
                      {isConfirmed && '✓ CONFIRMED (ভর্তি নিশ্চিত)'}
                      {isExpired && '✕ EXPIRED (মেয়াদোত্তীর্ণ)'}
                      {isCancelled && '⊘ CANCELLED (বাতিল)'}
                    </span>
                  </td>
                  <td className="time-text">
                    {new Date(h.expiresAt).toLocaleString('bn-BD', {
                      timeZone: 'UTC',
                      hour12: true
                    })}{' '}
                    (UTC)
                  </td>
                  <td>
                    <button
                      className="btn-action"
                      onClick={() => onSelectHold(h.holdId)}
                    >
                      {isHeld ? 'ফি দিন / বিস্তারিত →' : 'বিস্তারিত দেখুন'}
                    </button>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
};
