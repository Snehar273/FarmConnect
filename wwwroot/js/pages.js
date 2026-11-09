(function() {
    var $ = function(s, r) { return (r || document).querySelector(s); };
    var $$ = function(s, r) { return Array.prototype.slice.call((r || document).querySelectorAll(s)); };
    var reduce = matchMedia('(prefers-reduced-motion: reduce)').matches;
    var css = function(n) { return getComputedStyle(document.documentElement).getPropertyValue(n).trim(); };
    var anim = function() { return reduce ? false : { duration: 1400, easing: 'easeOutQuart' }; };
    var fmt = function(v, d) { return Number(v || 0).toLocaleString('en-IN', { minimumFractionDigits: d || 0, maximumFractionDigits: d || 0 }); };
    var pal = function() { return [css('--p2'), css('--amber'), css('--terra'), '#4a7a8c', '#9aa89a']; };
    var named = function() { return { Placed: css('--amber'), Accepted: css('--p2'), Completed: css('--p'), Rejected: css('--terra'), Cancelled: '#9aa89a', Active: css('--p2'), Inactive: '#9aa89a', SoldOut: css('--terra'), Assigned: css('--p2'), PendingAssignment: css('--amber'), Farmer: css('--p2'), Buyer: css('--amber'), Admin: css('--terra') }; };
    var nodata = function(cv) { if (cv.parentElement) cv.parentElement.innerHTML = '<div class="nodata">No data yet</div>'; };
    var fresh = function(cv) { var o = Chart.getChart(cv); if (o) o.destroy(); };
    var any = function(a) { return a.some(function(x) { return x > 0; }); };

    var B = {
        combo: function(s) {
            var cv = document.getElementById(s.id);
            if (!cv) return;
            fresh(cv);
            var p = pal(),
                m = css('--mute'),
                l = css('--line');
            new Chart(cv, {
                data: {
                    labels: s.labels,
                    datasets: [
                        { type: 'bar', label: s.bar.label, data: s.bar.data, backgroundColor: p[0] + 'bb', borderRadius: 8, yAxisID: 'y' },
                        { type: 'line', label: s.line.label, data: s.line.data, borderColor: p[1], backgroundColor: p[1], tension: .35, borderWidth: 3, pointRadius: 3, yAxisID: 'y1' }
                    ]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    animation: anim(),
                    interaction: { mode: 'index', intersect: false },
                    plugins: { legend: { position: 'bottom', labels: { color: m } } },
                    scales: { x: { grid: { display: false }, ticks: { color: m, maxTicksLimit: 7 } }, y: { grid: { color: l }, ticks: { color: m } }, y1: { position: 'right', grid: { drawOnChartArea: false }, ticks: { color: m, precision: 0 } } }
                }
            });
        },
        donut: function(s) {
            var cv = document.getElementById(s.id);
            if (!cv) return;
            fresh(cv);
            if (!any(s.d.data)) { nodata(cv); return; }
            var n = named(),
                p = pal();
            new Chart(cv, {
                type: 'doughnut',
                data: { labels: s.d.labels, datasets: [{ data: s.d.data, borderWidth: 0, hoverOffset: 8, backgroundColor: s.d.labels.map(function(x, i) { return n[x] || p[i % 5]; }) }] },
                options: { responsive: true, maintainAspectRatio: false, cutout: '66%', animation: anim(), plugins: { legend: { position: 'bottom', labels: { color: css('--mute'), boxWidth: 10 } } } }
            });
        },
        bars: function(s) {
            var cv = document.getElementById(s.id);
            if (!cv) return;
            fresh(cv);
            if (!any(s.d.data)) { nodata(cv); return; }
            var n = named(),
                p = pal(),
                m = css('--mute'),
                l = css('--line');
            new Chart(cv, {
                type: 'bar',
                data: { labels: s.d.labels, datasets: [{ data: s.d.data, borderRadius: 8, backgroundColor: s.d.labels.map(function(x, i) { return n[x] || p[i % 5]; }) }] },
                options: {
                    indexAxis: s.h ? 'y' : 'x',
                    responsive: true,
                    maintainAspectRatio: false,
                    animation: anim(),
                    plugins: { legend: { display: false } },
                    scales: { x: { grid: { display: !!s.h, color: l }, ticks: { color: m, precision: 0 } }, y: { grid: { display: !s.h, color: l }, ticks: { color: m, precision: 0 } } }
                }
            });
        },
        spark: function(s) {
            var cv = document.getElementById(s.id);
            if (!cv) return;
            fresh(cv);
            if (!any(s.data)) { nodata(cv); return; }
            var p = pal(),
                m = css('--mute'),
                l = css('--line');
            new Chart(cv, {
                type: 'line',
                data: { labels: s.labels, datasets: [{ label: s.label, data: s.data, borderColor: p[0], backgroundColor: p[0] + '30', fill: true, tension: .4, borderWidth: 3, pointRadius: 3, pointHoverRadius: 6 }] },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    animation: anim(),
                    interaction: { mode: 'index', intersect: false },
                    plugins: { legend: { display: false } },
                    scales: { x: { grid: { display: false }, ticks: { color: m, maxTicksLimit: 7 } }, y: { grid: { color: l }, ticks: { color: m } } }
                }
            });
        }
    };

    window.fcPage = function(cfg) {
        var jobs = [],
            drawn = [];
        ['combo', 'donut', 'bars', 'spark'].forEach(function(k) {
            (cfg[k] || []).forEach(function(s) { jobs.push({ k: k, s: s }); }); });
        var draw = function(j) { if (drawn.indexOf(j) < 0) drawn.push(j);
            B[j.k](j.s); };
        var io = new IntersectionObserver(function(es, o) {
            es.forEach(function(e) {
                if (!e.isIntersecting) return;
                var j = jobs.filter(function(x) { return x.s.id === e.target.id; })[0];
                if (j) draw(j);
                o.unobserve(e.target);
            });
        }, { threshold: .2 });
        jobs.forEach(function(j) { var el = document.getElementById(j.s.id); if (el) io.observe(el); });
        window.fcRender = function() { drawn.forEach(function(j) { B[j.k](j.s); }); };
    };

    // gauges fill when scrolled into view
    var gio = new IntersectionObserver(function(es, o) {
        es.forEach(function(e) { if (!e.isIntersecting) return;
            e.target.style.setProperty('--cv', e.target.getAttribute('data-v') || 0);
            o.unobserve(e.target); });
    }, { threshold: .3 });
    $$('.gauge').forEach(function(g) { if (g.hasAttribute('data-gk')) return; if (reduce) g.style.setProperty('--cv', g.getAttribute('data-v') || 0);
        else gio.observe(g); });

    // ---- admin live control room
    var live = $('[data-live]');
    if (!live) return;
    var url = live.getAttribute('data-url'),
        urls = { logi: live.getAttribute('data-logi'), orders: live.getAttribute('data-orders') };
    var EVERY = 10,
        left = EVERY,
        first = true,
        seen = {};
    var EM = { Placed: '📝', Accepted: '✅', Completed: '🎉', Rejected: '❌', Cancelled: '🚫' };

    var setNum = function(el, to, flash) {
        var pre = el.getAttribute('data-pre') || '',
            suf = el.getAttribute('data-suf') || '',
            dec = +(el.getAttribute('data-dec') || 0);
        var from = parseFloat(el.getAttribute('data-cur') || '0');
        el.setAttribute('data-cur', to);
        if (flash && from !== to) { el.classList.remove('flashk');
            void el.offsetWidth;
            el.classList.add('flashk'); }
        if (reduce || from === to) { el.textContent = pre + fmt(to, dec) + suf; return; }
        var t0 = performance.now();
        var f = function(t) { var k = Math.min(1, (t - t0) / 900);
            el.textContent = pre + fmt(from + (to - from) * (1 - Math.pow(1 - k, 3)), dec) + suf; if (k < 1) requestAnimationFrame(f); };
        requestAnimationFrame(f);
    };
    var gauge = function(key, val) {
        var v = Math.max(0, Math.min(100, Math.round(val)));
        $$('[data-gk="' + key + '"]').forEach(function(g) { g.setAttribute('data-v', v);
            g.style.setProperty('--cv', v); var t = $('text', g); if (t) t.textContent = v + '%'; });
    };
    var alerts = function(s) {
        var ul = $('#alerts');
        if (!ul) return;
        ul.innerHTML = '';
        var add = function(cls, ico, text, href) {
            var li = document.createElement('li');
            li.className = 'al ' + cls;
            var a = document.createElement(href ? 'a' : 'span');
            if (href) a.href = href;
            a.textContent = ico + ' ' + text;
            li.appendChild(a);
            ul.appendChild(li);
        };
        if (s.pendingLogi > 0) add('warn', '🚚', s.pendingLogi + ' transport booking(s) need a partner', urls.logi);
        if (s.outboxFailed > 0) add('bad', '📨', s.outboxFailed + ' background event(s) failed', urls.logi);
        if (s.placed > 0) add('info', '📝', s.placed + ' order(s) waiting for a farmer to respond', urls.orders);
        if (s.hits + s.sql > 5 && s.cacheRatio < 30) add('warn', '⚡', 'Price cache hit rate is low (' + s.cacheRatio + '%)');
        if (!ul.children.length) add('ok', '✅', 'All systems normal. Nothing needs your attention.');
    };
    var feed = function(s) {
        var ul = $('#feed');
        if (!ul) return;
        ul.innerHTML = '';
        s.recent.forEach(function(o, i) {
            var key = o.orderNo + o.status,
                isNew = !first && !seen[key];
            seen[key] = 1;
            var li = document.createElement('li');
            li.className = 'fi' + (isNew ? ' new' : '');
            li.style.animationDelay = (first ? i * 90 : 0) + 'ms';
            var e = document.createElement('span');
            e.className = 'em';
            e.textContent = EM[o.status] || '📦';
            var m = document.createElement('span');
            m.className = 'mn';
            var b = document.createElement('b');
            b.textContent = o.orderNo;
            m.appendChild(b);
            m.appendChild(document.createTextNode(' · ' + o.buyer + ' → ' + o.farmer + ' · ₹' + fmt(o.amount, 0)));
            var st = document.createElement('span');
            st.className = 'st st-' + o.status;
            st.textContent = o.status;
            var t = document.createElement('span');
            t.className = 't';
            t.textContent = o.at;
            li.appendChild(e);
            li.appendChild(m);
            li.appendChild(st);
            li.appendChild(t);
            ul.appendChild(li);
        });
        if (!s.recent.length) { var li2 = document.createElement('li');
            li2.className = 'fi';
            li2.textContent = 'No orders yet.';
            ul.appendChild(li2); }
    };
    var apply = function(s) {
        $$('[data-k]').forEach(function(el) { var k = el.getAttribute('data-k'); if (s[k] !== undefined) setNum(el, Number(s[k]), !first); });
        var sub = $('[data-sub="users"]');
        if (sub) sub.textContent = s.farmers + ' farmers · ' + s.buyers + ' buyers';
        var cov = s.bookings > 0 ? 100 * (s.bookings - s.pendingLogi) / s.bookings : 100;
        var h = 100 - (s.outboxFailed > 0 ? 30 : 0) - (s.outboxOpen > 5 ? 10 : 0) - Math.min(20, s.pendingLogi * 5) - ((s.hits + s.sql > 5 && s.cacheRatio < 30) ? 5 : 0);
        gauge('health', h);
        gauge('cacheRatio', s.cacheRatio);
        gauge('coverage', cov);
        var hl = $('#hlabel');
        if (hl) hl.textContent = h >= 90 ? 'Excellent' : (h >= 70 ? 'Good' : 'Needs attention');
        alerts(s);
        feed(s);
        first = false;
        var u = $('[data-upd]');
        if (u) u.textContent = 'Updated ' + new Date().toLocaleTimeString('en-IN');
    };
    var poll = function() {
        fetch(url, { credentials: 'same-origin', headers: { Accept: 'application/json' } })
            .then(function(r) { if (!r.ok) throw new Error('x'); return r.json(); })
            .then(apply)
            .catch(function() { var u = $('[data-upd]'); if (u) u.textContent = 'Connection lost. Retrying…'; });
    };

    var snap = $('#snap');
    if (snap) { try { apply(JSON.parse(snap.textContent)); } catch (e) { poll(); } } else { poll(); }

    var clk = $('#clk'),
        bar = $('#cdbar');
    setInterval(function() {
        if (clk) clk.textContent = new Date().toLocaleTimeString('en-IN');
        if (document.hidden) return;
        left--;
        if (bar) bar.style.width = ((EVERY - left) / EVERY * 100) + '%';
        if (left <= 0) { left = EVERY; if (bar) bar.style.width = '0%';
            poll(); }
    }, 1000);
    var rf = $('[data-refresh]');
    if (rf) rf.addEventListener('click', function() { left = EVERY;
        poll(); });
})();