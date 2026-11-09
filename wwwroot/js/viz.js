(function() {
    var $ = function(s, r) { return (r || document).querySelector(s); };
    var $$ = function(s, r) { return Array.prototype.slice.call((r || document).querySelectorAll(s)); };
    var reduce = matchMedia('(prefers-reduced-motion: reduce)').matches;
    var css = function(n) { return getComputedStyle(document.documentElement).getPropertyValue(n).trim(); };
    var fmt = function(v, d) { return Number(v || 0).toLocaleString('en-IN', { minimumFractionDigits: d || 0, maximumFractionDigits: d || 0 }); };
    var anim = function() { return reduce ? false : { duration: 1300, easing: 'easeOutQuart' }; };
    var pal = function() { return [css('--p2'), css('--amber'), css('--terra'), '#4a7a8c', '#9aa89a']; };
    var named = function() { return { Placed: css('--amber'), Accepted: css('--p2'), Completed: css('--p'), Rejected: css('--terra'), Cancelled: '#9aa89a', Active: css('--p2'), Inactive: '#9aa89a', SoldOut: css('--terra'), Assigned: css('--p2'), PendingAssignment: css('--amber'), Farmer: css('--p2'), Buyer: css('--amber'), Admin: css('--terra') }; };
    var anyv = function(a) { return a.some(function(x) { return Math.abs(x || 0) > 0; }); };
    var none = function(cv) { if (cv.parentElement) cv.parentElement.innerHTML = '<div class="nodata">No data yet</div>'; };

    // ---------- charts ----------
    var lineOpts = function(c, m, l, multi) {
        return {
            responsive: true,
            maintainAspectRatio: false,
            animation: anim(),
            interaction: { mode: 'index', intersect: false },
            plugins: { legend: { display: multi, position: 'bottom', labels: { color: m } } },
            scales: { x: { grid: { display: false }, ticks: { color: m, maxTicksLimit: 8 } }, y: { title: { display: !!c.y, text: c.y, color: m }, grid: { color: l }, ticks: { color: m } } }
        };
    };
    var B = {
        line: function(cv, c) {
            var p = pal(),
                m = css('--mute'),
                l = css('--line');
            return new Chart(cv, { type: 'line', data: { labels: c.labels, datasets: c.sets.map(function(s, i) { return { label: s.label, data: s.data, borderColor: p[i % 4], backgroundColor: p[i % 4] + '30', fill: false, tension: .38, borderWidth: 3, pointRadius: 3, pointHoverRadius: 6, spanGaps: true }; }) }, options: lineOpts(c, m, l, c.sets.length > 1) });
        },
        band: function(cv, c) {
            var p = pal(),
                m = css('--mute'),
                l = css('--line');
            return new Chart(cv, {
                type: 'line',
                data: {
                    labels: c.labels,
                    datasets: [
                        { label: 'Lowest', data: c.min, borderColor: p[1], borderDash: [5, 4], borderWidth: 2, pointRadius: 0, tension: .35, fill: false },
                        { label: 'Highest', data: c.max, borderColor: p[2], borderDash: [5, 4], borderWidth: 2, pointRadius: 0, tension: .35, fill: '-1', backgroundColor: p[0] + '26' },
                        { label: 'Modal (average)', data: c.modal, borderColor: p[0], borderWidth: 3, pointRadius: 3, pointHoverRadius: 6, tension: .35, fill: false }
                    ]
                },
                options: lineOpts(c, m, l, true)
            });
        },
        bar: function(cv, c) {
            var m = css('--mute'),
                l = css('--line'),
                p = pal(),
                n = named();
            var cols = c.signed ? c.data.map(function(v) { return v >= 0 ? p[0] : p[2]; }) : c.labels.map(function(x, i) { return n[x] || p[i % 5]; });
            var tk = { color: m };
            if (c.int) tk.precision = 0;
            return new Chart(cv, {
                type: 'bar',
                data: { labels: c.labels, datasets: [{ data: c.data, borderRadius: 8, backgroundColor: cols }] },
                options: {
                    indexAxis: c.h ? 'y' : 'x',
                    responsive: true,
                    maintainAspectRatio: false,
                    animation: anim(),
                    plugins: { legend: { display: false } },
                    scales: { x: { grid: { display: !!c.h, color: l }, ticks: { color: m, maxTicksLimit: 9 } }, y: { grid: { display: !c.h, color: l }, ticks: tk } }
                }
            });
        },
        donut: function(cv, c) {
            var n = named(),
                p = pal();
            return new Chart(cv, {
                type: 'doughnut',
                data: { labels: c.labels, datasets: [{ data: c.data, borderWidth: 0, hoverOffset: 8, backgroundColor: c.labels.map(function(x, i) { return n[x] || p[i % 5]; }) }] },
                options: { responsive: true, maintainAspectRatio: false, cutout: '66%', animation: anim(), plugins: { legend: { position: 'bottom', labels: { color: css('--mute'), boxWidth: 10 } } } }
            });
        },
        combo: function(cv, c) {
            var p = pal(),
                m = css('--mute'),
                l = css('--line'),
                t = { color: m };
            if (c.int) t.precision = 0;
            return new Chart(cv, {
                data: {
                    labels: c.labels,
                    datasets: [
                        { type: 'bar', label: c.bar.label, data: c.bar.data, backgroundColor: p[0] + 'bb', borderRadius: 8, yAxisID: 'y' },
                        { type: 'line', label: c.line.label, data: c.line.data, borderColor: p[1], backgroundColor: p[1], tension: .35, borderWidth: 3, pointRadius: 3, yAxisID: 'y1' }
                    ]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    animation: anim(),
                    interaction: { mode: 'index', intersect: false },
                    plugins: { legend: { position: 'bottom', labels: { color: m } } },
                    scales: { x: { grid: { display: false }, ticks: { color: m, maxTicksLimit: 7 } }, y: { grid: { color: l }, ticks: t }, y1: { position: 'right', grid: { drawOnChartArea: false }, ticks: { color: m } } }
                }
            });
        },
        float: function(cv, c) {
            var m = css('--mute'),
                l = css('--line');
            return new Chart(cv, {
                type: 'bar',
                data: { labels: c.labels, datasets: [{ data: c.data, borderRadius: 10, borderSkipped: false, backgroundColor: c.colors.map(function(x) { return css(x); }) }] },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    animation: anim(),
                    plugins: { legend: { display: false }, tooltip: { callbacks: { label: function(ctx) { var v = ctx.raw; return '₹' + fmt(Math.abs(v[1] - v[0]), 2); } } } },
                    scales: { x: { grid: { display: false }, ticks: { color: m } }, y: { grid: { color: l }, ticks: { color: m } } }
                }
            });
        }
    };
    var isEmpty = function(c) {
        if (c.empty) return true;
        if (c.type === 'band') return !anyv(c.modal);
        if (c.type === 'combo') return !anyv(c.bar.data.concat(c.line.data));
        if (c.type === 'line') return !c.sets.some(function(s) { return anyv(s.data); });
        if (c.type === 'float') return false;
        return !anyv(c.data);
    };

    window.fcViz = function(cfg) {
        if (!window.Chart) return;
        var list = cfg.charts || [],
            live = {};
        var draw = function(c, mode) {
            var cv = document.getElementById(c.id);
            if (!cv) return;
            var old = Chart.getChart(cv);
            if (old) old.destroy();
            var cc = c.modes ? c.modes[mode || c.def] : c;
            if (isEmpty(cc)) { none(cv); return; }
            live[c.id] = { c: c, mode: mode };
            B[cc.type](cv, cc);
        };
        var io = new IntersectionObserver(function(es, o) {
            es.forEach(function(e) {
                if (!e.isIntersecting) return;
                var c = list.filter(function(x) { return x.id === e.target.id; })[0];
                if (c) draw(c, c.def);
                o.unobserve(e.target);
            });
        }, { threshold: .2 });
        list.forEach(function(c) { var el = document.getElementById(c.id); if (el) io.observe(el); });
        $$('.seg button[data-chart]').forEach(function(b) {
            b.addEventListener('click', function() {
                var id = b.getAttribute('data-chart'),
                    mode = b.getAttribute('data-mode');
                $$('.seg button[data-chart="' + id + '"]').forEach(function(x) { x.classList.toggle('on', x === b);
                    x.setAttribute('aria-pressed', String(x === b)); });
                var c = list.filter(function(x) { return x.id === id; })[0];
                if (c) draw(c, mode);
            });
        });
        window.fcRender = function() { Object.keys(live).forEach(function(id) { draw(live[id].c, live[id].mode); }); };
    };

    // ---------- gauges ----------
    var gio = new IntersectionObserver(function(es, o) {
        es.forEach(function(e) { if (!e.isIntersecting) return;
            e.target.style.setProperty('--cv', e.target.getAttribute('data-v') || 0);
            o.unobserve(e.target); });
    }, { threshold: .3 });
    $$('.gauge').forEach(function(g) { if (g.hasAttribute('data-gk')) return; if (reduce) g.style.setProperty('--cv', g.getAttribute('data-v') || 0);
        else gio.observe(g); });

    // ---------- tips carousel ----------
    $$('[data-tipcar]').forEach(function(box) {
        var slides = $$('.tc-slide', box),
            dots = $$('.tc-dots button', box),
            bar = $('.tc-bar i', box),
            cnt = $('.tc-count', box);
        var cur = 0,
            timer = null,
            vis = true,
            hold = false,
            DUR = 5200;
        var restart = function() {
            if (!bar) return;
            bar.style.transition = 'none';
            bar.style.width = '0%';
            void bar.offsetWidth;
            if (!reduce && !hold) { bar.style.transition = 'width ' + DUR + 'ms linear';
                bar.style.width = '100%'; }
        };
        var go = function(n, dir) {
            n = (n + slides.length) % slides.length;
            if (n === cur) return;
            var d = dir || (n > cur ? 1 : -1),
                old = slides[cur];
            box.classList.add('nt');
            box.setAttribute('data-dir', d);
            void box.offsetWidth;
            box.classList.remove('nt');
            old.classList.remove('on');
            old.classList.add('out');
            slides[n].classList.remove('out');
            slides[n].classList.add('on');
            setTimeout(function() { old.classList.remove('out'); }, 700);
            dots.forEach(function(x, i) { x.classList.toggle('on', i === n); });
            if (cnt) cnt.textContent = (n + 1) + ' / ' + slides.length;
            cur = n;
            restart();
        };
        var start = function() { if (reduce || slides.length < 2) return;
            clearInterval(timer);
            timer = setInterval(function() { if (document.hidden || !vis || hold) return;
                go(cur + 1, 1); }, DUR);
            restart(); };
        var stop = function() { clearInterval(timer);
            timer = null;
            restart(); };
        var manual = function(n, d) { go(n, d);
            start(); };
        var nx = $('[data-next]', box),
            pv = $('[data-prev]', box);
        if (nx) nx.addEventListener('click', function() { manual(cur + 1, 1); });
        if (pv) pv.addEventListener('click', function() { manual(cur - 1, -1); });
        dots.forEach(function(d, i) { d.addEventListener('click', function() { manual(i); }); });
        box.addEventListener('mouseenter', function() { hold = true;
            stop(); });
        box.addEventListener('mouseleave', function() { hold = false;
            start(); });
        box.addEventListener('focusin', function() { hold = true;
            stop(); });
        box.addEventListener('focusout', function() { hold = false;
            start(); });
        box.addEventListener('keydown', function(e) { if (e.key === 'ArrowRight') manual(cur + 1, 1); if (e.key === 'ArrowLeft') manual(cur - 1, -1); });
        new IntersectionObserver(function(es) { vis = es[0].isIntersecting; }, { threshold: .2 }).observe(box);
        start();
    });

    // ---------- logistics: filter + route highlight ----------
    $$('[data-filter]').forEach(function(box) {
        var chips = $$('.fchips button', box),
            items = $$('[data-st]', box),
            q = $('input[type=search]', box),
            nn = $('.fnone', box),
            cur = 'all';
        var apply = function() {
            var t = (q ? q.value : '').toLowerCase().trim(),
                n = 0;
            items.forEach(function(it) { var ok = (cur === 'all' || it.getAttribute('data-st') === cur) && (!t || it.textContent.toLowerCase().indexOf(t) > -1);
                it.hidden = !ok; if (ok) n++; });
            if (nn) nn.hidden = n > 0 || items.length === 0;
        };
        chips.forEach(function(b) { b.addEventListener('click', function() { cur = b.getAttribute('data-f');
                chips.forEach(function(x) { x.classList.toggle('on', x === b); });
                apply(); }); });
        if (q) q.addEventListener('input', apply);
    });
    var map = $('.tnmap');
    if (map) {
        var hl = function(id) { map.classList.toggle('dim', !!id);
            $$('.rt', map).forEach(function(p) { p.classList.toggle('hl', !!id && p.getAttribute('data-rt') === id); }); };
        $$('.bk[data-rt]').forEach(function(row) {
            var id = row.getAttribute('data-rt');
            ['mouseenter', 'focus'].forEach(function(ev) { row.addEventListener(ev, function() { hl(id); }); });
            ['mouseleave', 'blur'].forEach(function(ev) { row.addEventListener(ev, function() { hl(null); }); });
        });
    }

    // ---------- forms: live validity, caps lock, checklist, wizard, loading ----------
    var PAT = function(i) { var p = i.getAttribute('data-val-regex-pattern'); try { return p ? new RegExp(p) : null; } catch (e) { return null; } };
    var fieldOk = function(i) {
        var v = i.value;
        if (!v.trim()) return false;
        if (i.type === 'email') return /^[^@\s]+@[^@\s]+\.[^@\s]+$/.test(v);
        var re = PAT(i);
        if (re && !re.test(v)) return false;
        if (i.id === 'ConfirmPassword') { var pw = document.getElementById('Password'); return !!pw && pw.value === v; }
        if (i.getAttribute('data-rules') === 'strong') return v.length >= 8 && /[a-z]/.test(v) && /[A-Z]/.test(v) && /\d/.test(v) && /[^A-Za-z0-9]/.test(v);
        return true;
    };
    var mark = function(i) {
        var ff = i.closest('.ff');
        if (!ff) return;
        var has = i.value.trim() !== '',
            ok = has && fieldOk(i);
        ff.classList.toggle('valid', ok);
        ff.classList.toggle('invalid', has && !ok);
        if (ok) { var m = $('.fmsg', i.closest('.fwrap')); if (m && m.getAttribute('data-own')) { m.textContent = '';
                m.removeAttribute('data-own'); } }
    };
    $$('.ff input.form-control').forEach(function(i) {
        ['input', 'blur'].forEach(function(ev) { i.addEventListener(ev, function() { mark(i); if (i.id === 'Password') { var c = document.getElementById('ConfirmPassword'); if (c && c.value) mark(c); } }); });
        mark(i);
    });
    $$('.ff.hasbtn input').forEach(function(i) {
        i.addEventListener('keyup', function(e) {
            var cap = $('.caps', i.closest('.fwrap'));
            if (!cap || !e.getModifierState) return;
            cap.hidden = !e.getModifierState('CapsLock');
        });
        i.addEventListener('blur', function() { var cap = $('.caps', i.closest('.fwrap')); if (cap) cap.hidden = true; });
    });
    var cl = $('.pwlist'),
        pwi = document.getElementById('Password');
    if (cl && pwi) {
        var rules = [function(v) { return v.length >= 8; }, function(v) { return /[a-z]/.test(v) && /[A-Z]/.test(v); }, function(v) { return /\d/.test(v); }, function(v) { return /[^A-Za-z0-9]/.test(v); }];
        var lis = $$('li', cl),
            up = function() { lis.forEach(function(li, k) { li.classList.toggle('ok', rules[k](pwi.value)); }); };
        pwi.addEventListener('input', up);
        up();
    }

    $$('form[data-loading]').forEach(function(f) {
        f.addEventListener('submit', function(e) {
            setTimeout(function() {
                if (e.defaultPrevented) return;
                var b = $('button.btn-fc[type=submit], button.btn-fc:not([type])', f);
                if (b && !b.hidden) { b.classList.add('loading'); }
            }, 0);
        });
    });

    var wz = $('[data-wizard]');
    if (wz) {
        var steps = $$('.wz-step', wz),
            ind = $$('.wz-steps .s', wz),
            links = $$('.wz-steps>i', wz),
            nextB = $('[data-next]', wz),
            backB = $('[data-back]', wz),
            subB = $('[data-sub]', wz),
            cur = 0;
        var show = function(n, dir) {
            steps.forEach(function(s, k) {
                s.hidden = k !== n;
                s.classList.remove('on', 'back');
                if (k === n) { void s.offsetWidth;
                    s.classList.add('on'); if (dir < 0) s.classList.add('back'); }
            });
            ind.forEach(function(s, k) { s.classList.toggle('on', k === n);
                s.classList.toggle('done', k < n); });
            links.forEach(function(l, k) { l.classList.toggle('on', k < n); });
            backB.style.visibility = n === 0 ? 'hidden' : 'visible';
            nextB.hidden = n === steps.length - 1;
            subB.hidden = n !== steps.length - 1;
            cur = n;
            var f = $('input.form-control', steps[n]);
            if (f && n > 0) setTimeout(function() { f.focus({ preventScroll: true }); }, 350);
        };
        var stepOk = function(i) {
            var st = steps[i],
                ok = true;
            if (i === 0) return !!$('input[name=Role]:checked', st);
            $$('.ff input.form-control', st).forEach(function(inp) {
                var req = inp.hasAttribute('data-val-required'),
                    has = inp.value.trim() !== '',
                    good = has ? fieldOk(inp) : !req;
                var ff = inp.closest('.ff');
                if (ff && (req || has)) {
                    ff.classList.toggle('invalid', !good);
                    ff.classList.toggle('valid', good && has);
                    if (!good) { ff.classList.remove('shk');
                        void ff.offsetWidth;
                        ff.classList.add('shk'); }
                }
                if (!good) {
                    ok = false;
                    var m = $('.fmsg', inp.closest('.fwrap'));
                    if (m && !m.textContent) { m.textContent = has ? (inp.getAttribute('data-hint') || 'Please check this value') : 'This field is required';
                        m.setAttribute('data-own', '1'); }
                }
            });
            return ok;
        };
        nextB.addEventListener('click', function() { if (stepOk(cur)) show(cur + 1, 1); });
        backB.addEventListener('click', function() { show(cur - 1, -1); });
        wz.addEventListener('keydown', function(e) {
            if (e.key === 'Enter' && cur < steps.length - 1 && e.target.tagName === 'INPUT' && e.target.type !== 'radio') { e.preventDefault();
                nextB.click(); }
        });
        wz.addEventListener('submit', function(e) {
            for (var k = 0; k < steps.length; k++) { if (!stepOk(k)) { e.preventDefault();
                    show(k, -1); return; } }
        });
        // after a server error, jump to the step that needs attention
        var bad = $('.field-validation-error', wz),
            summ = $('.validation-summary-errors', wz);
        if (bad) show(+bad.closest('.wz-step').getAttribute('data-step'), 1);
        else if (summ) show(/assword/.test(summ.textContent) ? 2 : 1, 1);
        else show(0, 1);
    }
})();