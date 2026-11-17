// صليب الأسنان (Palmer) واختيار الأسنان بترقيم FDI مع الجسور والأسنان اللبنية.
// الصيغة المخزّنة: "13-14p-15,21" — نفس صيغة Classes/TeethCode.cs على الخادم.
window.ToothPicker = (function () {
    'use strict';

    var ARCHES = [
        [18, 17, 16, 15, 14, 13, 12, 11, 21, 22, 23, 24, 25, 26, 27, 28],
        [48, 47, 46, 45, 44, 43, 42, 41, 31, 32, 33, 34, 35, 36, 37, 38],
        [55, 54, 53, 52, 51, 61, 62, 63, 64, 65],
        [85, 84, 83, 82, 81, 71, 72, 73, 74, 75]
    ];

    function locate(fdi) {
        for (var a = 0; a < ARCHES.length; a++) {
            var i = ARCHES[a].indexOf(fdi);
            if (i >= 0) return { arch: a, pos: i };
        }
        return null;
    }
    function isPrimary(fdi) { return Math.floor(fdi / 10) >= 5; }
    function quadrant(fdi) { var q = Math.floor(fdi / 10); return isPrimary(fdi) ? q - 4 : q; }
    function label(fdi) { return isPrimary(fdi) ? String.fromCharCode(64 + fdi % 10) : String(fdi % 10); }
    function sortByArch(teeth) {
        return teeth.slice().sort(function (a, b) {
            var la = locate(a.fdi), lb = locate(b.fdi);
            return la.arch - lb.arch || la.pos - lb.pos;
        });
    }

    // ───── قراءة / كتابة ─────
    function parse(text) {
        var groups = [];
        if (!text) return groups;
        String(text).split(',').forEach(function (token) {
            token = token.trim();
            if (!token) return;
            var teeth = token.split('-').map(function (p) {
                var m = /^\(?(\d{2})\)?(p)?$/i.exec(p.trim());
                return m ? { fdi: parseInt(m[1], 10), p: !!m[2] || p.trim().charAt(0) === '(' } : null;
            }).filter(function (t) { return t && locate(t.fdi); });
            if (teeth.length) groups.push({ teeth: sortByArch(teeth) });
        });
        return sortGroups(groups);
    }
    function sortGroups(groups) {
        return groups.sort(function (a, b) {
            var la = locate(a.teeth[0].fdi), lb = locate(b.teeth[0].fdi);
            return la.arch - lb.arch || la.pos - lb.pos;
        });
    }
    function serialize(groups) {
        return sortGroups(groups.slice()).map(function (g) {
            return g.teeth.map(function (t) { return t.fdi + (t.p && g.teeth.length > 1 ? 'p' : ''); }).join('-');
        }).join(',');
    }
    function count(groups) { return groups.reduce(function (n, g) { return n + g.teeth.length; }, 0); }
    function display(groups) {
        return groups.map(function (g) {
            return g.teeth.map(function (t) { return t.p && g.teeth.length > 1 ? '(' + t.fdi + ')' : String(t.fdi); }).join('-');
        }).join('، ');
    }

    /// تحقق الجسر: فك واحد، أسنان متجاورة، والطرفان سنان حقيقيان.
    function bridgeError(teeth) {
        if (teeth.length < 2) return 'الجسر يحتاج سنّين على الأقل.';
        var locs = teeth.map(function (t) { return locate(t.fdi); });
        var arch = locs[0].arch;
        if (locs.some(function (l) { return l.arch !== arch; })) return 'الجسر يجب أن يكون في فك واحد ومن نوع أسنان واحد (دائمة أو لبنية).';
        var pos = locs.map(function (l) { return l.pos; }).sort(function (a, b) { return a - b; });
        for (var i = 1; i < pos.length; i++)
            if (pos[i] !== pos[i - 1] + 1) return 'أسنان الجسر يجب أن تكون متجاورة — أضف الأسنان الناقصة ثم علّمها دمية.';
        var sorted = sortByArch(teeth);
        if (sorted[0].p || sorted[sorted.length - 1].p) return 'طرفا الجسر يجب أن يكونا سنّين حقيقيين (دعامتين) وليس دمية.';
        return null;
    }

    // ───── رسم الصليب ─────
    function escape(s) { return String(s).replace(/[&<>"]/g, function (c) { return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]; }); }

    function crossHtml(groups) {
        if (!groups.length) return '<span class="teeth-cross-empty">اختيار الأسنان</span>';
        var cells = { 1: [], 2: [], 3: [], 4: [] };
        groups.forEach(function (g) {
            var bridge = g.teeth.length > 1;
            g.teeth.forEach(function (t) {
                cells[quadrant(t.fdi)].push({ fdi: t.fdi, text: t.p && bridge ? '(' + label(t.fdi) + ')' : label(t.fdi), bridge: bridge, primary: isPrimary(t.fdi) });
            });
        });
        function cell(q) {
            var list = cells[q].slice().sort(function (a, b) {
                // الأقرب إلى خط المنتصف يكون بجانب الخط: يمين المريض معكوس
                var la = locate(a.fdi), lb = locate(b.fdi);
                return la.arch - lb.arch || la.pos - lb.pos;
            });
            return list.map(function (x) {
                return '<span class="tc-t' + (x.bridge ? ' tc-bridge' : '') + (x.primary ? ' tc-primary' : '') + '">' + escape(x.text) + '</span>';
            }).join('');
        }
        return '<span class="teeth-cross" dir="ltr">' +
            '<span class="tc-q tc-ur">' + cell(1) + '</span><span class="tc-q tc-ul">' + cell(2) + '</span>' +
            '<span class="tc-q tc-lr">' + cell(4) + '</span><span class="tc-q tc-ll">' + cell(3) + '</span></span>';
    }

    // ───── نافذة الاختيار ─────
    var modalEl, modal, state, onSave, dentition = 'permanent', pending = null;

    function teethOfQuadrant(q) {
        // الترتيب كما يُرى من أمام المريض: يمين المريض على اليسار، والأسنان من الخلف إلى الأمام نحو خط المنتصف
        var primary = dentition === 'primary';
        var max = primary ? 5 : 8, base = primary ? (q + 4) * 10 : q * 10;
        var list = [];
        for (var n = 1; n <= max; n++) list.push(base + n);
        return (q === 1 || q === 4) ? list.reverse() : list;
    }

    function groupOf(fdi) {
        for (var i = 0; i < state.length; i++)
            for (var j = 0; j < state[i].teeth.length; j++)
                if (state[i].teeth[j].fdi === fdi) return { g: state[i], t: state[i].teeth[j], index: i };
        return null;
    }

    function clickTooth(fdi) {
        setError('');
        var found = groupOf(fdi);
        if (pending) {
            // وضع الجسر: إضافة / إزالة من الجسر الجاري
            var idx = pending.findIndex(function (t) { return t.fdi === fdi; });
            if (idx >= 0) pending.splice(idx, 1);
            else {
                if (found && found.g.teeth.length > 1) { setError('هذا السن ضمن جسر آخر — فكّ ذلك الجسر أولاً.'); return; }
                if (found) state.splice(found.index, 1);
                pending.push({ fdi: fdi, p: false });
            }
        } else if (found && found.g.teeth.length > 1) {
            found.t.p = !found.t.p; // داخل جسر: تبديل سن حقيقي / دمية
        } else if (found) {
            state.splice(found.index, 1);
        } else {
            state.push({ teeth: [{ fdi: fdi, p: false }] });
        }
        render();
    }

    function setError(msg) {
        var el = modalEl.querySelector('.tp-error');
        el.textContent = msg || '';
        el.style.display = msg ? '' : 'none';
    }

    function render() {
        var pendingSet = {};
        (pending || []).forEach(function (t) { pendingSet[t.fdi] = true; });

        [1, 2, 3, 4].forEach(function (q) {
            var box = modalEl.querySelector('.tp-q' + q);
            box.innerHTML = '';
            teethOfQuadrant(q).forEach(function (fdi) {
                var found = groupOf(fdi);
                var b = document.createElement('button');
                b.type = 'button';
                b.className = 'tp-tooth';
                if (pendingSet[fdi]) b.classList.add('is-pending');
                else if (found && found.g.teeth.length > 1) b.classList.add(found.t.p ? 'is-pontic' : 'is-bridge');
                else if (found) b.classList.add('is-on');
                b.innerHTML = '<span>' + label(fdi) + '</span><small>' + fdi + '</small>';
                b.title = 'السن ' + fdi + (found && found.g.teeth.length > 1 ? (found.t.p ? ' — دمية' : ' — دعامة جسر') : '');
                b.addEventListener('click', function () { clickTooth(fdi); });
                box.appendChild(b);
            });
        });

        modalEl.querySelectorAll('.tp-dentition button').forEach(function (b) {
            b.classList.toggle('active', b.getAttribute('data-d') === dentition);
        });

        var bridgeBtn = modalEl.querySelector('.tp-bridge-start');
        var bridgeBar = modalEl.querySelector('.tp-bridge-bar');
        bridgeBtn.style.display = pending ? 'none' : '';
        bridgeBar.style.display = pending ? '' : 'none';

        // قائمة المختار مع حذف / فك الجسر
        var list = modalEl.querySelector('.tp-list');
        list.innerHTML = '';
        state.forEach(function (g, i) {
            var chip = document.createElement('span');
            chip.className = 'tp-chip' + (g.teeth.length > 1 ? ' is-bridge' : '');
            chip.innerHTML = (g.teeth.length > 1 ? 'جسر ' : '') + escape(display([g])) +
                ' <button type="button" aria-label="إزالة">×</button>';
            chip.querySelector('button').addEventListener('click', function () {
                if (g.teeth.length > 1) {
                    // فك الجسر: تبقى الدعامات أسناناً مفردة وتُحذف الدمية
                    state.splice(i, 1);
                    g.teeth.filter(function (t) { return !t.p; }).forEach(function (t) { state.push({ teeth: [{ fdi: t.fdi, p: false }] }); });
                } else state.splice(i, 1);
                render();
            });
            list.appendChild(chip);
        });

        var n = count(state);
        modalEl.querySelector('.tp-preview').innerHTML = crossHtml(state);
        modalEl.querySelector('.tp-summary').textContent = n ? (n + ' سن — ' + display(state)) : 'لم تُختر أسنان';
    }

    function ensureModal() {
        if (modalEl) return;
        modalEl = document.getElementById('toothPickerModal');
        modal = bootstrap.Modal.getOrCreateInstance(modalEl);

        modalEl.querySelectorAll('.tp-dentition button').forEach(function (b) {
            b.addEventListener('click', function () { dentition = b.getAttribute('data-d'); render(); });
        });
        modalEl.querySelector('.tp-bridge-start').addEventListener('click', function () { pending = []; setError(''); render(); });
        modalEl.querySelector('.tp-bridge-cancel').addEventListener('click', function () {
            (pending || []).forEach(function (t) { state.push({ teeth: [{ fdi: t.fdi, p: false }] }); });
            pending = null; setError(''); render();
        });
        modalEl.querySelector('.tp-bridge-done').addEventListener('click', function () {
            var err = bridgeError(pending);
            if (err) { setError(err); return; }
            state.push({ teeth: sortByArch(pending) });
            pending = null; setError(''); render();
        });
        modalEl.querySelector('.tp-clear').addEventListener('click', function () { state = []; pending = null; setError(''); render(); });
        modalEl.querySelector('.tp-save').addEventListener('click', function () {
            if (pending && pending.length) { setError('أنهِ الجسر الجاري أو ألغِه أولاً.'); return; }
            for (var i = 0; i < state.length; i++) {
                if (state[i].teeth.length > 1) {
                    var err = bridgeError(state[i].teeth);
                    if (err) { setError(err); return; }
                }
            }
            var value = serialize(state);
            modal.hide();
            if (onSave) onSave(value, count(state));
        });
    }

    function open(value, saveCallback) {
        ensureModal();
        state = parse(value);
        pending = null;
        onSave = saveCallback;
        dentition = state.length && state.every(function (g) { return isPrimary(g.teeth[0].fdi); }) ? 'primary' : 'permanent';
        setError('');
        render();
        modal.show();
    }

    return { open: open, parse: parse, serialize: serialize, count: count, display: display, crossHtml: crossHtml };
})();
