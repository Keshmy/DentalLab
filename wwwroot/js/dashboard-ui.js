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

// خلايا الجداول: الأرقام والمبالغ والتواريخ تبقى على سطر واحد، وباقي النص يلتف (انظر site.css).
(function ($) {
    'use strict';

    var compact = /^[\s\d٠-٩.,،:\/\-+%×()]+$/;

    function markCells(root) {
        $(root).find('.table td, .table th').each(function () {
            var text = (this.textContent || '').trim();
            if (text.length > 0 && text.length <= 24 && compact.test(text)) {
                this.classList.add('cell-nowrap');
            }
        });
    }

    $(function () {
        markCells(document);
        // DataTables يعيد رسم الصفوف عند التنقل بين الصفحات أو البحث.
        $(document).on('draw.dt', function (e) { markCells(e.target); });
    });
})(jQuery);

// قوائم قابلة للبحث (Tom Select): الطبيب والعيادة في كل الصفحات، وأي <select class="js-searchable">.
// حقل نصي فيه data-suggest-url يصبح حقلاً باقتراحات (مثل اسم الحالة) مع السماح بكتابة اسم جديد.
(function ($) {
    'use strict';

    if (typeof TomSelect === 'undefined') return;

    // توحيد الكتابة العربية للبحث: أ/إ/آ ← ا، ة ← ه، ى ← ي، وحذف التشكيل والتطويل.
    function norm(s) {
        return (s || '').toString().toLowerCase()
            .replace(/[\u064B-\u0652\u0640]/g, '')
            .replace(/[أإآٱ]/g, 'ا')
            .replace(/ة/g, 'ه')
            .replace(/ى/g, 'ي')
            .replace(/\s+/g, ' ')
            .trim();
    }

    function score(search) {
        var q = norm(search);
        return function (item) {
            if (!q) return 1;
            return norm(item.text).indexOf(q) > -1 ? 1 : 0;
        };
    }

    var texts = {
        no_results: function () { return '<div class="no-results">لا توجد نتائج</div>'; },
        option_create: function (data, escape) { return '<div class="create">إضافة: <strong>' + escape(data.input) + '</strong></div>'; }
    };

    function enhanceSelect(el) {
        if (el.tomselect || el.multiple) return;
        var empty = el.querySelector('option[value=""]');
        new TomSelect(el, {
            plugins: ['dropdown_input'],
            allowEmptyOption: true,
            placeholder: empty ? empty.textContent.trim() : '',
            maxOptions: 500,
            score: score,
            render: texts,
            onDropdownOpen: function () {
                var input = this.dropdown.querySelector('.dropdown-input');
                if (input) input.setAttribute('placeholder', 'بحث...');
            }
        });
    }

    function enhanceSuggest(el) {
        if (el.tomselect) return;
        var url = el.getAttribute('data-suggest-url');
        new TomSelect(el, {
            plugins: ['dropdown_input'],
            maxItems: 1,
            create: true,
            createOnBlur: true,
            persist: false,
            valueField: 'value',
            labelField: 'value',
            searchField: [],
            score: score,
            render: texts,
            shouldLoad: function (q) { return q.length >= 1; },
            load: function (q, callback) {
                fetch(url + (url.indexOf('?') > -1 ? '&' : '?') + 'q=' + encodeURIComponent(q),
                    { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
                    .then(function (r) { return r.ok ? r.json() : []; })
                    .then(function (names) {
                        callback(names.map(function (n) { return { value: n, text: n }; }));
                    })
                    .catch(function () { callback(); });
            },
            onDropdownOpen: function () {
                var input = this.dropdown.querySelector('.dropdown-input');
                if (input) input.setAttribute('placeholder', 'اكتب للبحث أو لإضافة اسم جديد...');
            }
        });
    }

    $(function () {
        document.querySelectorAll('select').forEach(function (el) {
            var name = (el.getAttribute('name') || '').toLowerCase();
            if (name === 'doctorid' || name === 'clinicid' || el.classList.contains('js-searchable'))
                enhanceSelect(el);
        });
        document.querySelectorAll('input[data-suggest-url]').forEach(enhanceSuggest);
    });
})(jQuery);
