import React, { useState } from 'react';
import { api } from '../services/api';
import { NotificationMessage } from './NotificationBanner';

interface ClassCreateModalProps {
  isOpen: boolean;
  onClose: () => void;
  onClassCreated: () => void;
  setNotification: (notif: NotificationMessage | null) => void;
}

export const ClassCreateModal: React.FC<ClassCreateModalProps> = ({
  isOpen,
  onClose,
  onClassCreated,
  setNotification
}) => {
  const [name, setName] = useState('');
  const [totalSeats, setTotalSeats] = useState<number | ''>('');
  const [loading, setLoading] = useState(false);

  if (!isOpen) return null;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    if (!name.trim()) {
      setNotification({ type: 'warning', text: 'ক্লাসের নাম আবশ্যক।' });
      return;
    }

    const seatsNum = Number(totalSeats);
    if (!seatsNum || seatsNum <= 0) {
      setNotification({ type: 'warning', text: 'মোট আসন সংখ্যা ১ বা তার বেশি হতে হবে।' });
      return;
    }

    try {
      setLoading(true);
      const created = await api.createClass({
        name: name.trim(),
        totalSeats: seatsNum
      });

      setNotification({
        type: 'success',
        text: `নতুন ক্লাস "${created.name}" (${created.totalSeats}টি আসন) সফলভাবে তৈরি হয়েছে।`
      });

      setName('');
      setTotalSeats('');
      onClassCreated();
      onClose();
    } catch (err: unknown) {
      const error = err as { detail?: string };
      setNotification({
        type: 'error',
        text: error.detail || 'ক্লাস তৈরি করতে সমস্যা হয়েছে।'
      });
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="modal-backdrop">
      <div className="modal-card">
        <div className="modal-header">
          <h3>নতুন শ্রেণি ও শিফট যোগ করুন (Admin)</h3>
          <button className="close-btn" onClick={onClose} disabled={loading}>
            &times;
          </button>
        </div>

        <form onSubmit={handleSubmit}>
          <div className="form-group">
            <label htmlFor="class-name">শ্রেণির নাম ও শিফট:</label>
            <input
              id="class-name"
              type="text"
              placeholder="যেমন: Class 6 - Morning Shift"
              value={name}
              onChange={(e) => setName(e.target.value)}
              required
              disabled={loading}
            />
            <small>প্রতিটি শিফট আলাদা একটি ক্লাস হিসেবে তৈরি হবে।</small>
          </div>

          <div className="form-group">
            <label htmlFor="total-seats">মোট আসন সংখ্যা (Total Seats):</label>
            <input
              id="total-seats"
              type="number"
              min="1"
              placeholder="যেমন: 120"
              value={totalSeats}
              onChange={(e) => setTotalSeats(e.target.value === '' ? '' : parseInt(e.target.value, 10))}
              required
              disabled={loading}
            />
          </div>

          <div className="modal-actions">
            <button type="button" className="btn-secondary" onClick={onClose} disabled={loading}>
              বাতিল
            </button>
            <button type="submit" className="btn-primary" disabled={loading}>
              {loading ? 'তৈরি হচ্ছে...' : 'ক্লাস সংরক্ষণ করুন'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};
