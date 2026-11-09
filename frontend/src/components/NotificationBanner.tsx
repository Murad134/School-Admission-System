import React from 'react';

export interface NotificationMessage {
  type: 'success' | 'error' | 'warning' | 'info';
  text: string;
}

interface NotificationBannerProps {
  notification: NotificationMessage | null;
  onClose: () => void;
}

export const NotificationBanner: React.FC<NotificationBannerProps> = ({ notification, onClose }) => {
  if (!notification) return null;

  const bgStyles = {
    success: 'notification-success',
    error: 'notification-error',
    warning: 'notification-warning',
    info: 'notification-info'
  }[notification.type];

  return (
    <div className={`notification-banner ${bgStyles}`}>
      <div className="notification-content">
        <span className="notification-icon">
          {notification.type === 'success' && '✓'}
          {notification.type === 'error' && '✕'}
          {notification.type === 'warning' && '⚠'}
          {notification.type === 'info' && 'ℹ'}
        </span>
        <span className="notification-text">{notification.text}</span>
      </div>
      <button className="notification-close-btn" onClick={onClose} aria-label="Close notification">
        &times;
      </button>
    </div>
  );
};
