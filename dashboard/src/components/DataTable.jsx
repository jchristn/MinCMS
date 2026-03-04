import { useState, useMemo, useEffect, useRef } from 'react';
import './DataTable.css';

const DataTable = ({ columns, data, loading, onAction, onRefresh, actions = [], onRowClick, selectable, rowKey, onSelectionChange, rowActions, isRowSelectable }) => {
  const [sortField, setSortField] = useState(null);
  const [sortDirection, setSortDirection] = useState('asc');
  const [selectedKeys, setSelectedKeys] = useState(new Set());
  const prevSelectedRef = useRef([]);

  const handleSort = (key) => {
    if (sortField === key) {
      setSortDirection(sortDirection === 'asc' ? 'desc' : 'asc');
    } else {
      setSortField(key);
      setSortDirection('asc');
    }
  };

  const sortedData = useMemo(() => {
    if (!sortField || !data) return data || [];
    return [...data].sort((a, b) => {
      let aVal = a[sortField];
      let bVal = b[sortField];
      if (aVal == null) return 1;
      if (bVal == null) return -1;
      if (typeof aVal === 'string') aVal = aVal.toLowerCase();
      if (typeof bVal === 'string') bVal = bVal.toLowerCase();
      if (aVal < bVal) return sortDirection === 'asc' ? -1 : 1;
      if (aVal > bVal) return sortDirection === 'asc' ? 1 : -1;
      return 0;
    });
  }, [data, sortField, sortDirection]);

  const selectableData = useMemo(() => {
    return isRowSelectable ? sortedData.filter(isRowSelectable) : sortedData;
  }, [sortedData, isRowSelectable]);

  const toggleSelection = (key) => {
    setSelectedKeys((prev) => {
      const next = new Set(prev);
      if (next.has(key)) next.delete(key);
      else next.add(key);
      return next;
    });
  };

  const toggleAll = () => {
    if (!selectableData.length) return;
    const allKeys = selectableData.map((r) => r[rowKey]);
    const allSelected = allKeys.every((k) => selectedKeys.has(k));
    setSelectedKeys(allSelected ? new Set() : new Set(allKeys));
  };

  // Notify parent when selection changes
  useEffect(() => {
    if (!selectable || !onSelectionChange || !data) return;
    const selected = data.filter((r) => selectedKeys.has(r[rowKey]));
    // Avoid calling if the selection hasn't actually changed
    const keys = selected.map((r) => r[rowKey]).join(',');
    const prevKeys = prevSelectedRef.current.map((r) => r[rowKey]).join(',');
    if (keys !== prevKeys) {
      prevSelectedRef.current = selected;
      onSelectionChange(selected);
    }
  }, [selectedKeys, selectable, onSelectionChange, data, rowKey]);

  // Clear selection when data changes (e.g. after delete/refresh)
  useEffect(() => {
    setSelectedKeys(new Set());
  }, [data]);

  const extraCols = (selectable ? 1 : 0) + (actions.length > 0 ? 1 : 0);

  return (
    <div className="dt-container">
      <div className="dt-toolbar">
        <span className="dt-count">{data ? data.length : 0} record(s)</span>
        {onRefresh && (
          <button className="btn btn-secondary dt-refresh" onClick={onRefresh} title="Refresh">
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><polyline points="23 4 23 10 17 10"></polyline><polyline points="1 20 1 14 7 14"></polyline><path d="M3.51 9a9 9 0 0 1 14.85-3.36L23 10M1 14l4.64 4.36A9 9 0 0 0 20.49 15"></path></svg>
          </button>
        )}
      </div>
      <div className="dt-wrapper">
        <table className="dt-table">
          <thead>
            <tr>
              {selectable && (
                <th className="dt-checkbox-col">
                  <input
                    type="checkbox"
                    checked={selectableData.length > 0 && selectableData.every((r) => selectedKeys.has(r[rowKey]))}
                    onChange={toggleAll}
                  />
                </th>
              )}
              {columns.map((col) => (
                <th
                  key={col.key}
                  onClick={col.sortable !== false ? () => handleSort(col.key) : undefined}
                  className={col.sortable !== false ? 'dt-sortable' : ''}
                >
                  {col.label}
                  {sortField === col.key && (
                    <span className="dt-sort-icon">{sortDirection === 'asc' ? ' \u25B2' : ' \u25BC'}</span>
                  )}
                </th>
              ))}
              {actions.length > 0 && <th className="dt-actions-header">Actions</th>}
            </tr>
          </thead>
          <tbody>
            {loading ? (
              Array.from({ length: 5 }).map((_, i) => (
                <tr key={`skel-${i}`}>
                  {selectable && <td><div className="dt-skeleton"></div></td>}
                  {columns.map((col, j) => (
                    <td key={j}><div className="dt-skeleton"></div></td>
                  ))}
                  {actions.length > 0 && <td><div className="dt-skeleton"></div></td>}
                </tr>
              ))
            ) : sortedData.length === 0 ? (
              <tr>
                <td colSpan={columns.length + extraCols} className="dt-empty">
                  No data found
                </td>
              </tr>
            ) : (
              sortedData.map((row, rowIdx) => {
                const key = rowKey ? row[rowKey] : rowIdx;
                const canSelect = !isRowSelectable || isRowSelectable(row);
                const isSelected = selectable && canSelect && selectedKeys.has(key);
                const rowActs = rowActions ? rowActions(row) : actions;
                return (
                  <tr
                    key={key}
                    className={`${onRowClick ? 'dt-clickable' : ''}${isSelected ? ' dt-selected' : ''}`}
                    onClick={onRowClick ? () => onRowClick(row) : undefined}
                  >
                    {selectable && (
                      <td className="dt-checkbox-col">
                        {canSelect ? (
                          <input
                            type="checkbox"
                            checked={isSelected}
                            onChange={() => toggleSelection(key)}
                            onClick={(e) => e.stopPropagation()}
                          />
                        ) : null}
                      </td>
                    )}
                    {columns.map((col) => (
                      <td key={col.key}>
                        {col.render ? col.render(row[col.key], row) : (row[col.key] ?? '-')}
                      </td>
                    ))}
                    {actions.length > 0 && (
                      <td className="dt-actions-cell">
                        {rowActs.map((action) => (
                          <button
                            key={action.name}
                            className={`btn btn-sm ${action.className || 'btn-secondary'}`}
                            onClick={(e) => { e.stopPropagation(); onAction && onAction(action.name, row); }}
                            title={action.label}
                          >
                            {typeof action.icon === 'function' ? action.icon(row) : (action.icon || action.label)}
                          </button>
                        ))}
                      </td>
                    )}
                  </tr>
                );
              })
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
};

export default DataTable;
