import React from 'react';
import ReactDOM from 'react-dom/client';
import { AppContent } from './App';
import { StudentProvider } from './context/StudentContext';
import './index.css';

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <StudentProvider>
      <AppContent />
    </StudentProvider>
  </React.StrictMode>
);

