import Topbar from './Topbar.jsx';
import CollectionList from './CollectionList.jsx';
import ObservabilityPanel from './ObservabilityPanel.jsx';
import './Dashboard.css';

const Dashboard = () => {
  return (
    <div className="dashboard">
      <Topbar />
      <div className="dashboard-content">
        <CollectionList />
        <ObservabilityPanel />
      </div>
    </div>
  );
};

export default Dashboard;
