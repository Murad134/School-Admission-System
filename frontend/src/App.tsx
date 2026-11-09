import React, { useState } from 'react';
import { Header } from './components/Header';
import { ClassList } from './components/ClassList';
import { MyHolds } from './components/MyHolds';
import { ClassCreateModal } from './components/ClassCreateModal';
import { HoldDetailModal } from './components/HoldDetailModal';
import { NotificationBanner, NotificationMessage } from './components/NotificationBanner';
import { useStudent } from './context/StudentContext';

export const AppContent: React.FC = () => {
  const { activeHoldId } = useStudent();
  const [currentTab, setCurrentTab] = useState<'classes' | 'my-holds'>('classes');
  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);
  const [selectedHoldId, setSelectedHoldId] = useState<string | null>(activeHoldId);
  const [notification, setNotification] = useState<NotificationMessage | null>(null);
  const [refreshTrigger, setRefreshTrigger] = useState(0);

  const handleHoldCreated = (holdId: string) => {
    setSelectedHoldId(holdId);
  };

  const handleCloseHoldModal = () => {
    setSelectedHoldId(null);
  };

  const handleHoldStatusChanged = () => {
    setRefreshTrigger((prev) => prev + 1);
  };

  return (
    <div className="app-container">
      <Header
        currentTab={currentTab}
        onTabChange={setCurrentTab}
        onOpenCreateClass={() => setIsCreateModalOpen(true)}
      />

      <main className="main-content">
        <NotificationBanner
          notification={notification}
          onClose={() => setNotification(null)}
        />

        {currentTab === 'classes' ? (
          <ClassList
            key={refreshTrigger}
            onHoldCreated={handleHoldCreated}
            setNotification={setNotification}
          />
        ) : (
          <MyHolds
            key={refreshTrigger}
            onSelectHold={(holdId) => setSelectedHoldId(holdId)}
            setNotification={setNotification}
          />
        )}
      </main>

      <footer className="app-footer">
        <p>
          School Admission System &bull; অনন‍্য প্রযুক্তি লিমিটেড (Onnorokom Projukti Limited) &bull;
          Take-Home Assignment
        </p>
      </footer>

      {/* Modals */}
      <ClassCreateModal
        isOpen={isCreateModalOpen}
        onClose={() => setIsCreateModalOpen(false)}
        onClassCreated={() => setRefreshTrigger((prev) => prev + 1)}
        setNotification={setNotification}
      />

      <HoldDetailModal
        holdId={selectedHoldId}
        onClose={handleCloseHoldModal}
        onStatusChanged={handleHoldStatusChanged}
        setNotification={setNotification}
      />
    </div>
  );
};
