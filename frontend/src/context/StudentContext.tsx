import React, { createContext, useContext, useState } from 'react';

interface StudentContextType {
  studentId: string;
  setStudentId: (id: string) => void;
  isValidStudentId: boolean;
  activeHoldId: string | null;
  setActiveHoldId: (holdId: string | null) => void;
}

const StudentContext = createContext<StudentContextType | undefined>(undefined);

export const StudentProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [studentId, setStudentIdState] = useState<string>(() => {
    return localStorage.getItem('admission_student_id') || '';
  });

  const [activeHoldId, setActiveHoldIdState] = useState<string | null>(() => {
    return localStorage.getItem('admission_active_hold_id') || null;
  });

  const setStudentId = (id: string) => {
    const sanitized = id.replace(/\D/g, '').slice(0, 17);
    setStudentIdState(sanitized);
    localStorage.setItem('admission_student_id', sanitized);
  };

  const setActiveHoldId = (holdId: string | null) => {
    setActiveHoldIdState(holdId);
    if (holdId) {
      localStorage.setItem('admission_active_hold_id', holdId);
    } else {
      localStorage.removeItem('admission_active_hold_id');
    }
  };

  const isValidStudentId = /^[0-9]{17}$/.test(studentId);

  return (
    <StudentContext.Provider
      value={{
        studentId,
        setStudentId,
        isValidStudentId,
        activeHoldId,
        setActiveHoldId
      }}
    >
      {children}
    </StudentContext.Provider>
  );
};

export const useStudent = (): StudentContextType => {
  const context = useContext(StudentContext);
  if (!context) {
    throw new Error('useStudent must be used within a StudentProvider');
  }
  return context;
};
