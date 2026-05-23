import { useState, useMemo, useEffect, useRef, useLayoutEffect } from 'react';
import { createPortal } from 'react-dom';
import './DataTable.css';

const menuTriggerIcon = (
  <svg width="16" height="16" viewBox="0 0 24 24" fill="currentColor" aria-hidden="true">
    <circle cx="12" cy="5" r="1.8" />
    <circle cx="12" cy="12" r="1.8" />
    <circle cx="12" cy="19" r="1.8" />
  </svg>
);

const DataTable = ({
  columns,
  data,
  loading,
  onAction,
  onRefresh,
  actions = [],
  onRowClick,
  selectable,
  rowKey,
  onSelectionChange,
  rowActions,
  isRowSelectable,
  actionMode = 'buttons',
  actionsHeaderLabel = 'Actions',
}) => {
  const [sortField, setSortField] = useState(null);
  const [sortDirection, setSortDirection] = useState('asc');
  const [selectedKeys, setSelectedKeys] = useState(new Set());
  const [openMenu, setOpenMenu] = useState(null);
  const [menuPosition, setMenuPosition] = useState(null);
  const prevSelectedRef = useRef([]);
  const menuRef = useRef(null);
  const isMenuMode = actionMode === 'menu';

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

  useEffect(() => {
    setOpenMenu(null);
    setMenuPosition(null);
  }, [data]);

  useLayoutEffect(() => {
    if (!openMenu?.anchorEl || !menuRef.current) return undefined;

    const updateMenuPosition = () => {
      if (!openMenu.anchorEl || !menuRef.current) return;

      const anchorRect = openMenu.anchorEl.getBoundingClientRect();
      const menuRect = menuRef.current.getBoundingClientRect();
      const viewportWidth = window.innerWidth;
      const viewportHeight = window.innerHeight;
      const margin = 12;
      const gap = 8;

      let top = anchorRect.bottom + gap;
      let originY = 'top';

      if (top + menuRect.height > viewportHeight - margin) {
        top = anchorRect.top - menuRect.height - gap;
        originY = 'bottom';
      }

      top = Math.max(margin, Math.min(top, viewportHeight - menuRect.height - margin));

      let left = anchorRect.right - menuRect.width;
      if (left < margin) {
        left = anchorRect.left;
      }
      left = Math.max(margin, Math.min(left, viewportWidth - menuRect.width - margin));

      setMenuPosition({ top, left, originY });
    };

    updateMenuPosition();
    window.addEventListener('resize', updateMenuPosition);
    window.addEventListener('scroll', updateMenuPosition, true);

    return () => {
      window.removeEventListener('resize', updateMenuPosition);
      window.removeEventListener('scroll', updateMenuPosition, true);
    };
  }, [openMenu]);

  useEffect(() => {
    if (!openMenu?.anchorEl) return undefined;

    const handlePointerDown = (event) => {
      const target = event.target;
      if (menuRef.current?.contains(target) || openMenu.anchorEl.contains(target)) return;
      setOpenMenu(null);
      setMenuPosition(null);
    };

    const handleEscape = (event) => {
      if (event.key === 'Escape') {
        setOpenMenu(null);
        setMenuPosition(null);
      }
    };

    document.addEventListener('pointerdown', handlePointerDown);
    document.addEventListener('keydown', handleEscape);

    return () => {
      document.removeEventListener('pointerdown', handlePointerDown);
      document.removeEventListener('keydown', handleEscape);
    };
  }, [openMenu]);

  const renderActionIcon = (action, row) => {
    return typeof action.icon === 'function' ? action.icon(row) : (action.icon || action.label);
  };

  const handleMenuToggle = (event, key, row, rowActs) => {
    event.stopPropagation();

    if (openMenu?.key === key) {
      setOpenMenu(null);
      setMenuPosition(null);
      return;
    }

    setOpenMenu({
      key,
      row,
      rowActs,
      anchorEl: event.currentTarget,
    });
    setMenuPosition(null);
  };

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
              {actions.length > 0 && <th className="dt-actions-header">{actionsHeaderLabel}</th>}
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
                        {isMenuMode ? (
                          rowActs.length > 0 ? (
                            <button
                              type="button"
                              className={`dt-actions-menu-trigger${openMenu?.key === key ? ' is-open' : ''}`}
                              onClick={(event) => handleMenuToggle(event, key, row, rowActs)}
                              aria-haspopup="menu"
                              aria-expanded={openMenu?.key === key}
                              aria-label={`Open ${actionsHeaderLabel.toLowerCase()} menu`}
                            >
                              {menuTriggerIcon}
                            </button>
                          ) : (
                            <span className="dt-actions-empty">-</span>
                          )
                        ) : (
                          rowActs.map((action) => (
                            <button
                              key={action.name}
                              className={`btn btn-sm ${action.className || 'btn-secondary'}`}
                              onClick={(e) => { e.stopPropagation(); onAction && onAction(action.name, row); }}
                              title={action.label}
                            >
                              {renderActionIcon(action, row)}
                            </button>
                          ))
                        )}
                      </td>
                    )}
                  </tr>
                );
              })
            )}
          </tbody>
        </table>
      </div>
      {isMenuMode && openMenu && menuPosition && createPortal(
        <div
          ref={menuRef}
          className="dt-context-menu"
          style={{ top: `${menuPosition.top}px`, left: `${menuPosition.left}px` }}
          data-origin-y={menuPosition.originY}
          role="menu"
        >
          {openMenu.rowActs.map((action) => (
            <button
              key={action.name}
              type="button"
              className={`dt-context-menu-item${action.className?.includes('danger') ? ' is-danger' : ''}`}
              onClick={(event) => {
                event.stopPropagation();
                setOpenMenu(null);
                setMenuPosition(null);
                onAction && onAction(action.name, openMenu.row);
              }}
              role="menuitem"
            >
              <span className="dt-context-menu-icon">{renderActionIcon(action, openMenu.row)}</span>
              <span className="dt-context-menu-label">{action.label}</span>
            </button>
          ))}
        </div>,
        document.body
      )}
    </div>
  );
};

export default DataTable;
