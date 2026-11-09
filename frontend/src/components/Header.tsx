import React from 'react';
import { useStudent } from '../context/StudentContext';

interface HeaderProps {
  currentTab: 'classes' | 'my-holds';
  onTabChange: (tab: 'classes' | 'my-holds') => void;
  onOpenCreateClass: () => void;
}

export const Header: React.FC<HeaderProps> = ({ currentTab, onTabChange, onOpenCreateClass }) => {
  const { studentId, setStudentId, isValidStudentId } = useStudent();

  return (
    <header className="app-header">
      <div className="header-top">
        <div className="brand">
          <div className="brand-badge">ভর্তি কার্যক্রম ২০২৬</div>
          <h1>School Admission System</h1>
          <p className="subtitle">অনলাইন বিদ্যালয় ভর্তি পোর্টাল (আগে আবেদন করলে আগে পাবেন)</p>
        </div>

        <div className="student-id-container">
          <label htmlFor="student-id-input">
            শিক্ষার্থীর জন্ম নিবন্ধন নম্বর (১৭ অঙ্ক):
          </label>
          <div className="input-wrapper">
            <input
              id="student-id-input"
              type="text"
              inputMode="numeric"
              placeholder="১৭ অঙ্কের জন্ম নিবন্ধন নম্বর দিন"
              value={studentId}
              onChange={(e) => setStudentId(e.target.value)}
              className={isValidStudentId ? 'input-valid' : studentId ? 'input-invalid' : ''}
              maxLength={17}
            />
            <span className={`id-counter ${isValidStudentId ? 'counter-valid' : ''}`}>
              {studentId.length}/17 {isValidStudentId ? '✓' : ''}
            </span>
          </div>
          {!isValidStudentId && studentId.length > 0 && (
            <span className="validation-hint">অনুগ্রহ করে ১৭ অঙ্কের সঠিক জন্ম নিবন্ধন নম্বর দিন</span>
          )}
          {isValidStudentId && (
            <span className="validation-success">✓ শিক্ষার্থীর পরিচয় সক্রিয় আছে</span>
          )}
        </div>
      </div>

      <nav className="header-nav">
        <div className="nav-tabs">
          <button
            className={`tab-btn ${currentTab === 'classes' ? 'active' : ''}`}
            onClick={() => onTabChange('classes')}
          >
            🏫 ভর্তির শ্রেণি ও শিফট তালিকা (Classes)
          </button>
          <button
            className={`tab-btn ${currentTab === 'my-holds' ? 'active' : ''}`}
            onClick={() => onTabChange('my-holds')}
          >
            📋 আমার আবেদনসমূহ (My Applications)
          </button>
        </div>

        <button className="admin-btn" onClick={onOpenCreateClass}>
          + নতুন ক্লাস তৈরি (Admin)
        </button>
      </nav>
    </header>
  );
};

