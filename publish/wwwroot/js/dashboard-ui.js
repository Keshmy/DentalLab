(function ($) {
    'use strict';

    // New key so old "collapsed=1" does not keep the sidebar permanently hidden
    var STORAGE_KEY = 'dentallab.sidebar.hidden.v2';
    var body = document.body;
    var DESKTOP = 992;

    function isDesktop() {
        return window.innerWidth >= DESKTOP;
    }

    function getSidebar() {
        return document.getElementById('sidebar');
    }

    function getToggleBtn() {
        return document.getElementById('labSidebarToggle');
    }

    function syncToggleUi(hiddenOrClosed) {
        var btn = getToggleBtn();
        if (!btn) return;
        btn.setAttribute('aria-expanded', hiddenOrClosed ? 'false' : 'true');
        var icon = btn.querySelector('.mdi');
        if (icon) {
            icon.classList.toggle('mdi-menu', !!hiddenOrClosed);
            icon.classList.toggle('mdi-close', !hiddenOrClosed && !isDesktop());
            // desktop: menu icon always; mobile open state uses close
            if (isDesktop()) {
                icon.classList.add('mdi-menu');
                icon.classList.remove('mdi-close');
            }
        }
    }

    function getBackdrop() {
        var el = document.getElementById('sidebarBackdrop');
        if (!el) {
            el = document.createElement('div');
            el.id = 'sidebarBackdrop';
            el.className = 'sidebar-backdrop';
            document.body.appendChild(el);
            el.addEventListener('click', closeMobileSidebar);
        }
        return el;
    }

    function openMobileSidebar() {
        var sidebar = getSidebar();
        if (!sidebar) return;
        sidebar.classList.add('active');
        getBackdrop().classList.add('show');
        body.classList.add('sidebar-mobile-open');
        syncToggleUi(false);
    }

    function closeMobileSidebar() {
        var sidebar = getSidebar();
        if (sidebar) sidebar.classList.remove('active');
        var backdrop = document.getElementById('sidebarBackdrop');
        if (backdrop) backdrop.classList.remove('show');
        body.classList.remove('sidebar-mobile-open');
        syncToggleUi(true);
    }

    function toggleMobileSidebar() {
        var sidebar = getSidebar();
        if (!sidebar) return;
        if (sidebar.classList.contains('active')) closeMobileSidebar();
        else openMobileSidebar();
    }

    function setDesktopHidden(hidden) {
        body.classList.remove('sidebar-icon-only');
        body.classList.toggle('sidebar-hidden', !!hidden);
        try { localStorage.setItem(STORAGE_KEY, hidden ? '1' : '0'); } catch (e) { }
        syncToggleUi(!!hidden);
        setTimeout(adjustTables, 280);
    }

    function toggleDesktopSidebar() {
        setDesktopHidden(!body.classList.contains('sidebar-hidden'));
    }

    function adjustTables() {
        if ($.fn.dataTable) {
            $.fn.dataTable.tables({ visible: true, api: true }).columns.adjust();
        }
    }

    // Default desktop: sidebar OPEN unless user hid it.
    // Default mobile: drawer CLOSED.
    if (isDesktop()) {
        if (localStorage.getItem(STORAGE_KEY) === '1') {
            body.classList.add('sidebar-hidden');
            syncToggleUi(true);
        } else {
            body.classList.remove('sidebar-hidden', 'sidebar-icon-only');
            syncToggleUi(false);
        }
    } else {
        body.classList.remove('sidebar-hidden', 'sidebar-icon-only');
        syncToggleUi(true);
    }

    // Clear legacy key that left the sidebar stuck closed
    try { localStorage.removeItem('dentallab.sidebar.collapsed'); } catch (e) { }

    $(function () {
        $(document).off('click', '[data-toggle="minimize"]');

        if (!isDesktop()) closeMobileSidebar();

        $(document).on('click', '[data-lab-sidebar-toggle]', function (e) {
            e.preventDefault();
            e.stopPropagation();
            if (isDesktop()) toggleDesktopSidebar();
            else toggleMobileSidebar();
        });

        $(document).on('click', '#sidebar a.nav-link', function () {
            var isParent = $(this).attr('data-toggle') === 'collapse' || $(this).attr('data-bs-toggle') === 'collapse';
            if (!isDesktop() && !isParent) closeMobileSidebar();
        });

        $(window).on('resize', function () {
            if (isDesktop()) {
                closeMobileSidebar();
                if (localStorage.getItem(STORAGE_KEY) === '1') setDesktopHidden(true);
                else setDesktopHidden(false);
            } else {
                body.classList.remove('sidebar-hidden', 'sidebar-icon-only');
                closeMobileSidebar();
            }
            adjustTables();
        });
    });
})(jQuery);
