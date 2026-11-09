(function() {
    var $ = function(s, r) { return (r || document).querySelector(s); };
    var $$ = function(s, r) { return Array.prototype.slice.call((r || document).querySelectorAll(s)); };
    var reduce = matchMedia('(prefers-reduced-motion: reduce)').matches;
    var hover = matchMedia('(hover:hover)').matches;

    // cursor spotlight on cards and tables
    $$('.kpi,.cbox,.stat,.panel,.ocard,.hitem,.table-wrap,.tile').forEach(function(el) { el.classList.add('glowc'); });
    document.addEventListener('pointermove', function(e) {
        var t = e.target;
        if (!(t instanceof Element)) return;
        var s = t.closest('.glowc,.spot');
        if (!s) return;
        var r = s.getBoundingClientRect();
        s.style.setProperty('--sx', (e.clientX - r.left) + 'px');
        s.style.setProperty('--sy', (e.clientY - r.top) + 'px');
    });

    // parallax + glow on banners and auth scenes
    if (hover && !reduce) {
        $$('[data-px]').forEach(function(sc) {
            var raf = 0,
                px = 0,
                gx = '50%',
                gy = '30%';
            sc.addEventListener('mousemove', function(e) {
                var r = sc.getBoundingClientRect();
                px = ((e.clientX - r.left) / r.width - 0.5) * 2;
                gx = ((e.clientX - r.left) / r.width * 100).toFixed(1) + '%';
                gy = ((e.clientY - r.top) / r.height * 100).toFixed(1) + '%';
                if (raf) return;
                raf = requestAnimationFrame(function() {
                    raf = 0;
                    sc.style.setProperty('--mx', px.toFixed(3));
                    sc.style.setProperty('--gx', gx);
                    sc.style.setProperty('--gy', gy);
                });
            });
            sc.addEventListener('mouseleave', function() { sc.style.setProperty('--mx', 0); });
        });
    }

    // back to top
    var tt = $('#toTop');
    if (tt) {
        addEventListener('scroll', function() { tt.classList.toggle('show', scrollY > 500); }, { passive: true });
    }
    $$('#toTop,[data-top]').forEach(function(b) {
        b.addEventListener('click', function() { scrollTo({ top: 0, behavior: reduce ? 'auto' : 'smooth' }); });
    });

    // rotating tips
    $$('[data-tips]').forEach(function(box) {
        var tips = $$('.tip', box),
            i = 0;
        if (tips.length < 2) return;
        setInterval(function() {
            if (document.hidden || reduce) return;
            tips[i].classList.remove('on');
            i = (i + 1) % tips.length;
            tips[i].classList.add('on');
        }, 4500);
    });

    // ---- interactive FAQ
    var fq = $('#faq2');
    if (fq) {
        var items = $$('.fq', fq),
            cur = -1,
            auto = true,
            visible = false,
            seen = false;
        var search = $('#fqs'),
            cats = $$('.fq-cats button', fq),
            empty = $('.fq-empty', fq),
            cat = 'all';

        var split = function(p) {
            if (p.getAttribute('data-w') || reduce) return;
            p.setAttribute('data-w', '1');
            var parts = p.textContent.split(' ');
            p.textContent = '';
            parts.forEach(function(w, i) {
                var s = document.createElement('span');
                s.style.setProperty('--i', i);
                s.textContent = w + ' ';
                p.appendChild(s);
            });
        };
        var openItem = function(i) {
            items.forEach(function(x, k) {
                var o = k === i;
                x.classList.toggle('open', o);
                $('.fq-q', x).setAttribute('aria-expanded', String(o));
                if (o) {
                    var p = $('.fa p', x);
                    split(p);
                    p.classList.remove('typ');
                    void p.offsetWidth;
                    p.classList.add('typ');
                }
            });
            cur = i;
        };
        var manual = function() {
            auto = false;
            fq.classList.add('manual');
        };

        items.forEach(function(x, i) {
            $('.fq-q', x).addEventListener('click', function() {
                manual();
                if (x.classList.contains('open')) {
                    x.classList.remove('open');
                    this.setAttribute('aria-expanded', 'false');
                    cur = -1;
                } else openItem(i);
            });
            $$('.vote button', x).forEach(function(b) {
                b.addEventListener('click', function() {
                    var em = $('.vote em', x);
                    em.textContent = b.getAttribute('data-v') === 'up' ? 'Thanks! Glad it helped.' : 'Thanks for letting us know.';
                    em.style.animation = 'none';
                    void em.offsetWidth;
                    em.style.animation = '';
                });
            });
        });

        var apply = function() {
            var q = (search.value || '').toLowerCase().trim(),
                shown = 0;
            items.forEach(function(x) {
                var ok = (cat === 'all' || x.getAttribute('data-c') === cat) && (!q || x.textContent.toLowerCase().indexOf(q) > -1);
                if (ok && x.hidden) {
                    x.style.animation = 'none';
                    void x.offsetWidth;
                    x.style.animation = '';
                }
                x.hidden = !ok;
                if (ok) shown++;
            });
            empty.hidden = shown > 0;
        };
        search.addEventListener('input', function() {
            manual();
            apply();
        });
        cats.forEach(function(b) {
            b.addEventListener('click', function() {
                manual();
                cat = b.getAttribute('data-c');
                cats.forEach(function(x) { x.classList.toggle('on', x === b); });
                apply();
            });
        });

        new IntersectionObserver(function(es) {
            visible = es[0].isIntersecting;
            if (visible && !seen) {
                seen = true;
                setTimeout(function() { if (auto) openItem(0); }, 700);
            }
        }, { threshold: 0.3 }).observe(fq);

        setInterval(function() {
            if (!auto || !visible || reduce || document.hidden) return;
            var vis = items.filter(function(x) { return !x.hidden; });
            if (!vis.length) return;
            var at = vis.indexOf(items[cur]);
            openItem(items.indexOf(vis[(at + 1) % vis.length]));
        }, 6500);
    }
})();