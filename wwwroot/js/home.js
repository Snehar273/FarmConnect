(function() {
    var $ = function(s, r) { return (r || document).querySelector(s); };
    var $$ = function(s, r) { return Array.prototype.slice.call((r || document).querySelectorAll(s)); };
    var reduce = matchMedia('(prefers-reduced-motion: reduce)').matches;
    var hover = matchMedia('(hover:hover)').matches;
    var inr = function(v) { return Number(v || 0).toLocaleString('en-IN', { maximumFractionDigits: 2 }); };

    // cursor spotlight
    document.addEventListener('pointermove', function(e) {
        var t = e.target;
        if (!(t instanceof Element)) return;
        var s = t.closest('.spot');
        if (!s) return;
        var r = s.getBoundingClientRect();
        s.style.setProperty('--sx', (e.clientX - r.left) + 'px');
        s.style.setProperty('--sy', (e.clientY - r.top) + 'px');
    });

    // ---- auto-playing showcase
    var show = $('#show');
    if (show) {
        var tabs = $$('.sc-tabs button', show),
            panes = $$('.sc-pane', show),
            buyT = null;
        var buyDemo = function(on) {
            clearInterval(buyT);
            buyT = null;
            if (!on || reduce) return;
            var q = $('#dq'),
                t = $('#dt'),
                m = $('#dm'),
                vals = [50, 100, 150, 100, 200],
                k = 0;
            var step = function() {
                var v = vals[k++ % vals.length];
                q.textContent = v;
                t.textContent = '₹' + inr(v * 25);
                m.textContent = '✓ Valid quantity';
                q.classList.remove('bmp');
                void q.offsetWidth;
                q.classList.add('bmp');
            };
            step();
            buyT = setInterval(step, 1500);
        };
        var go = function(i) {
            tabs.forEach(function(b) { b.classList.remove('on');
                b.setAttribute('aria-selected', 'false'); });
            void show.offsetWidth;
            tabs[i].classList.add('on');
            tabs[i].setAttribute('aria-selected', 'true');
            panes.forEach(function(p, k) { p.classList.toggle('on', k === i); });
            buyDemo(i === 1);
        };
        tabs.forEach(function(b, i) {
            b.addEventListener('click', function() { go(i); });
            $('.pg', b).addEventListener('animationend', function(e) { if (e.animationName === 'hx-pg') go((i + 1) % tabs.length); });
        });
        $('.sc-tabs', show).addEventListener('keydown', function(e) {
            if (e.key !== 'ArrowRight' && e.key !== 'ArrowLeft') return;
            var cur = tabs.findIndex(function(b) { return b.classList.contains('on'); });
            var n = (cur + (e.key === 'ArrowRight' ? 1 : -1) + tabs.length) % tabs.length;
            go(n);
            tabs[n].focus();
        });
        var pause = function(v) { return function() { show.classList.toggle('hov', v); }; };
        show.addEventListener('mouseenter', pause(true));
        show.addEventListener('mouseleave', pause(false));
        show.addEventListener('focusin', pause(true));
        show.addEventListener('focusout', pause(false));
        new IntersectionObserver(function(es) { show.classList.toggle('off', !es[0].isIntersecting); }, { threshold: 0.2 }).observe(show);
    }

    // ---- order simulator (runs only in the browser; nothing is saved)
    var sim = $('#sim');
    if (sim) {
        var MIN = 50,
            STOCK = 500,
            PRICE = 25,
            stock = STOCK,
            busy = false,
            timers = [];
        var rg = $('#sq', sim),
            qv = $('#sqv', sim),
            tot = $('#stot', sim),
            msg = $('#smsg', sim),
            btn = $('#sgo', sim),
            rst = $('#srst', sim);
        var bar = $('#sbar', sim),
            left = $('#sleft', sim),
            road = $('.road', sim),
            steps = $$('.pl li', sim);
        var upd = function() {
            var v = +rg.value,
                ok = v >= MIN && v <= stock;
            qv.textContent = v;
            tot.textContent = '₹' + inr(v * PRICE);
            msg.textContent = stock === 0 ? 'Sold out. The listing is now marked Sold Out.' : v < MIN ? 'Below the minimum order (' + MIN + ' kg)' : v > stock ? 'Only ' + stock + ' kg available' : '✓ Valid quantity';
            msg.className = 'smsg ' + (ok ? 'ok' : 'bad');
            var remain = ok ? stock - v : stock;
            bar.style.setProperty('--w', (remain / STOCK * 100) + '%');
            left.textContent = remain + ' kg left' + (ok ? ' after this order' : '');
            btn.disabled = !ok || busy;
        };
        var run = function() {
            var v = +rg.value;
            if (busy || v < MIN || v > stock) return;
            busy = true;
            btn.disabled = true;
            steps.forEach(function(s) { s.classList.remove('done', 'now'); });
            var d = reduce ? 0 : 750;
            steps.forEach(function(s, k) {
                timers.push(setTimeout(function() {
                    steps.forEach(function(x, j) { x.classList.toggle('done', j < k);
                        x.classList.toggle('now', j === k); });
                    if (k === 0) stock -= v;
                    if (k === 2) road.classList.add('go');
                    if (k === steps.length - 1) timers.push(setTimeout(function() { s.classList.remove('now');
                        s.classList.add('done');
                        busy = false;
                        upd(); }, reduce ? 0 : 700));
                    upd();
                }, k * d));
            });
        };
        var reset = function() {
            timers.forEach(clearTimeout);
            timers = [];
            stock = STOCK;
            busy = false;
            steps.forEach(function(s) { s.classList.remove('done', 'now'); });
            road.classList.remove('go');
            upd();
        };
        rg.addEventListener('input', upd);
        btn.addEventListener('click', run);
        rst.addEventListener('click', reset);
        upd();
    }

    // ---- flip cards (hover flips on desktop; tap flips on touch)
    $$('.flip').forEach(function(c) {
        var tog = function() { if (!hover) c.classList.toggle('fl'); };
        c.addEventListener('click', tog);
        c.addEventListener('keydown', function(e) {
            if (e.key === 'Enter' || e.key === ' ') { e.preventDefault();
                c.classList.toggle('fl'); }
        });
    });

    // ---- 3D tilt on listing cards
    if (hover && !reduce) {
        $$('#fresh .fc-card').forEach(function(card) {
            var lk = $('.fc-link', card);
            if (!lk) return;
            card.addEventListener('mousemove', function(e) {
                if (card.querySelector('.is-open')) { lk.style.transform = ''; return; }
                var r = card.getBoundingClientRect(),
                    x = (e.clientX - r.left) / r.width - 0.5,
                    y = (e.clientY - r.top) / r.height - 0.5;
                lk.style.transform = 'perspective(800px) rotateY(' + (x * 7) + 'deg) rotateX(' + (-y * 7) + 'deg)';
            });
            card.addEventListener('mouseleave', function() { lk.style.transform = ''; });
        });
    }
})();