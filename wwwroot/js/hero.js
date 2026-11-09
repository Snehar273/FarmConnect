(function() {
    var $ = function(s, r) { return (r || document).querySelector(s); };
    var $$ = function(s, r) { return Array.prototype.slice.call((r || document).querySelectorAll(s)); };
    var reduce = matchMedia('(prefers-reduced-motion: reduce)').matches;
    var hover = matchMedia('(hover:hover)').matches;
    var clamp = function(v, a, b) { return Math.max(a, Math.min(b, v)); };

    var sc = $('#scene');
    if (sc) {
        // mouse + scroll parallax and cursor glow
        var mx = 0,
            sy = 0,
            raf = 0;
        var apply = function() { raf = 0;
            sc.style.setProperty('--mx', mx.toFixed(3));
            sc.style.setProperty('--sy', sy.toFixed(1)); };
        var queue = function() { if (!raf) raf = requestAnimationFrame(apply); };
        if (hover && !reduce) {
            sc.addEventListener('mousemove', function(e) {
                var r = sc.getBoundingClientRect();
                mx = ((e.clientX - r.left) / r.width - 0.5) * 2;
                sc.style.setProperty('--gx', ((e.clientX - r.left) / r.width * 100).toFixed(1) + '%');
                sc.style.setProperty('--gy', ((e.clientY - r.top) / r.height * 100).toFixed(1) + '%');
                queue();
            });
            sc.addEventListener('mouseleave', function() { mx = 0;
                queue(); });
        }
        if (!reduce) addEventListener('scroll', function() { sy = clamp(scrollY, 0, 700);
            queue(); }, { passive: true });

        // live listing card carousel
        var cards = $$('.lc', sc),
            dots = $$('.dots button', sc),
            cur = 0,
            timer = null;
        var show = function(n) {
            cards.forEach(function(c, i) {
                c.classList.toggle('on', i === n);
                c.classList.toggle('prev', i !== n && i === (n - 1 + cards.length) % cards.length);
            });
            dots.forEach(function(d, i) { d.classList.toggle('on', i === n); });
            cur = n;
        };
        if (cards.length > 1) {
            var stop = function() { clearInterval(timer);
                timer = null; };
            var start = function() { if (!reduce && !timer) timer = setInterval(function() { show((cur + 1) % cards.length); }, 3800); };
            dots.forEach(function(d, i) { d.addEventListener('click', function() { stop();
                    show(i);
                    start(); }); });
            sc.addEventListener('mouseenter', stop);
            sc.addEventListener('mouseleave', start);
            start();
        }
    }

    // scroll-linked order flow
    var fl = $('#flow');
    if (fl) {
        var bar = $('.flowbar', fl),
            steps = $$('.st2', fl);
        var upd = function() {
            var r = fl.getBoundingClientRect();
            var p = reduce ? 1 : clamp((innerHeight * 0.8 - r.top) / (r.height * 0.9), 0, 1);
            bar.style.setProperty('--fp', (p * 100).toFixed(1) + '%');
            steps.forEach(function(s, i) { s.classList.toggle('on', p >= (i + 0.4) / steps.length); });
        };
        addEventListener('scroll', upd, { passive: true });
        addEventListener('resize', upd);
        upd();
    }
})();