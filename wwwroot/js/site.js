window.__fc = true;
(function() {
    var $ = function(s, r) { return (r || document).querySelector(s); };
    var $$ = function(s, r) { return Array.prototype.slice.call((r || document).querySelectorAll(s)); };
    var on = function(el, ev, fn) { if (el) el.addEventListener(ev, fn); };
    var root = document.documentElement;
    var reduce = matchMedia('(prefers-reduced-motion: reduce)').matches;
    var hover = matchMedia('(hover:hover)').matches;
    var inr = function(v, d) {
        if (d === undefined) d = 2;
        return Number(v || 0).toLocaleString('en-IN', { minimumFractionDigits: d, maximumFractionDigits: d });
    };

    // ---- theme
    var paint = function() {
        var b = $('#themeBtn'),
            t = root.dataset.theme;
        if (b) {
            b.textContent = t === 'dark' ? '☀️' : '🌙';
            b.setAttribute('aria-label', t === 'dark' ? 'Switch to light mode' : 'Switch to dark mode');
        }
    };
    paint();
    on($('#themeBtn'), 'click', function() {
        var t = root.dataset.theme === 'dark' ? 'light' : 'dark';
        root.dataset.theme = t;
        root.dataset.bsTheme = t;
        try { localStorage.setItem('fc-theme', t); } catch (e) {}
        paint();
        if (window.fcRender) window.fcRender();
    });

    // ---- scroll progress + nav shadow
    var bar = $('.scrollbar'),
        nav = $('.fc-nav');
    addEventListener('scroll', function() {
        var h = root.scrollHeight - innerHeight;
        if (bar) bar.style.transform = 'scaleX(' + (h > 0 ? scrollY / h : 0) + ')';
        if (nav) nav.classList.toggle('scrolled', scrollY > 8);
    }, { passive: true });

    // ---- nav sliding underline
    var nl = $('.navbar-nav'),
        ind = $('.nav-ind');
    if (nl && ind) {
        var act = function() { return $('.nav-link.active', nl); };
        var mv = function(el) {
            if (!el || innerWidth < 992) { ind.style.opacity = 0; return; }
            var r = el.getBoundingClientRect(),
                p = nl.getBoundingClientRect();
            ind.style.opacity = 1;
            ind.style.width = (r.width - 16) + 'px';
            ind.style.transform = 'translateX(' + (r.left - p.left + 8) + 'px)';
        };
        mv(act());
        $$('.nav-link', nl).forEach(function(l) {
            on(l, 'mouseenter', function() { mv(l); });
            on(l, 'focus', function() { mv(l); });
        });
        on(nl, 'mouseleave', function() { mv(act()); });
        addEventListener('resize', function() { mv(act()); });
    }

    // ---- reveal on scroll
    var io = new IntersectionObserver(function(es, o) {
        es.forEach(function(e) {
            if (!e.isIntersecting) return;
            e.target.classList.add('in');
            o.unobserve(e.target);
            setTimeout(function() { e.target.style.transitionDelay = ''; }, 1100);
        });
    }, { threshold: 0.12 });
    var initReveal = function(r) {
        var n = 0;
        $$('.reveal:not(.in)', r).forEach(function(el) {
            if (reduce) { el.classList.add('in'); return; }
            el.style.transitionDelay = ((n++) % 8) * 70 + 'ms';
            io.observe(el);
        });
    };
    initReveal();

    // ---- count-up numbers
    var cio = new IntersectionObserver(function(es, o) {
        es.forEach(function(e) {
            if (!e.isIntersecting) return;
            o.unobserve(e.target);
            var el = e.target,
                to = parseFloat(el.dataset.count) || 0,
                d = +(el.dataset.dec || 0),
                pre = el.dataset.pre || '';
            if (reduce) { el.textContent = pre + inr(to, d); return; }
            var t0 = performance.now();
            var f = function(t) {
                var k = Math.min(1, (t - t0) / 1200);
                el.textContent = pre + inr(to * (1 - Math.pow(1 - k, 3)), d);
                if (k < 1) requestAnimationFrame(f);
            };
            requestAnimationFrame(f);
        });
    });
    $$('[data-count]').forEach(function(el) { cio.observe(el); });

    // ---- toasts
    $$('.toast-fc').forEach(function(t, i) {
        var close = function() {
            t.classList.add('out');
            setTimeout(function() { t.remove(); }, 400);
        };
        on($('button', t), 'click', close);
        setTimeout(close, 5500 + i * 600);
    });

    // ---- produce card quantity selector (posts to existing Orders/Place; server re-validates)
    var buyOf = function(el) { return el.closest('[data-buy]'); };
    var calc = function(b) {
        if (!b) return false;
        var q = $('input[name=quantity]', b);
        if (!q) return false;
        var v = parseFloat(q.value) || 0,
            p = +b.dataset.price,
            mn = +b.dataset.min,
            mx = +b.dataset.max,
            u = b.dataset.unit || '',
            ok = v >= mn && v <= mx;
        var total = $('[data-total]', b),
            msg = $('.buy-msg', b),
            sub = $('[data-submit]', b);
        if (total) total.textContent = '₹' + inr(v * p);
        if (msg) {
            msg.textContent = v < mn ? 'Minimum order is ' + mn + ' ' + u : (v > mx ? 'Only ' + mx + ' ' + u + ' available' : '✓ Valid quantity');
            msg.className = 'buy-msg ' + (ok ? 'ok' : 'bad');
        }
        if (sub) sub.disabled = !ok;
        return ok;
    };
    var closeAll = function() {
        $$('[data-buy].is-open').forEach(function(x) {
            x.classList.remove('is-open');
            var ob = $('[data-open]', x);
            if (ob) ob.setAttribute('aria-expanded', 'false');
        });
    };

    document.addEventListener('click', function(e) {
        var t = e.target;
        if (!(t instanceof Element)) return;

        var o = t.closest('[data-open]');
        if (o) {
            var b = buyOf(o);
            if (!b) return;
            var open = !b.classList.contains('is-open');
            closeAll();
            if (open) {
                b.classList.add('is-open');
                o.setAttribute('aria-expanded', 'true');
                calc(b);
                setTimeout(function() {
                    var qi = $('input[name=quantity]', b);
                    if (qi) qi.focus({ preventScroll: true });
                }, 300);
            }
            return;
        }

        var s = t.closest('[data-inc],[data-dec]');
        if (s) {
            var b2 = buyOf(s),
                q2 = b2 && $('input[name=quantity]', b2);
            if (b2 && q2) {
                var st = +b2.dataset.step || 1;
                var nv = (parseFloat(q2.value) || +b2.dataset.min) + (s.hasAttribute('data-inc') ? st : -st);
                q2.value = +Math.min(+b2.dataset.max, Math.max(+b2.dataset.min, nv)).toFixed(2);
                calc(b2);
            }
        }

        var pw = t.closest('[data-pw]');
        if (pw) {
            var pi = $('input', pw.parentElement);
            if (pi) {
                var show = pi.type === 'password';
                pi.type = show ? 'text' : 'password';
                pw.textContent = show ? '🙈' : '👁️';
                pw.setAttribute('aria-pressed', String(show));
            }
        }

        var a = t.closest('a[data-swap]');
        if (a && !e.ctrlKey && !e.metaKey) {
            e.preventDefault();
            swap(a.href);
        }

        var fl = t.closest('[data-fill]');
        if (fl) {
            var parts = (fl.dataset.fill || '').split('|'),
                em = $('#Email'),
                pa = $('#Password');
            if (em) em.value = parts[0] || '';
            if (pa) pa.value = parts[1] || '';
            [em, pa].forEach(function(i) { if (i) i.dispatchEvent(new Event('input', { bubbles: true })); });
        }
    });

    document.addEventListener('input', function(e) {
        if (e.target.matches('[data-buy] input[name=quantity]')) calc(buyOf(e.target));
    });
    document.addEventListener('keydown', function(e) {
        if (e.key === 'Escape') {
            var o = $('[data-buy].is-open [data-open]');
            closeAll();
            if (o) o.focus();
        }
    });
    document.addEventListener('submit', function(e) {
        var b = e.target.closest('[data-buy]');
        if (!b) return;
        if (!calc(b)) { e.preventDefault(); return; }
        var s = $('[data-submit]', b);
        if (s) setTimeout(function() {
            s.disabled = true;
            s.textContent = 'Placing order…';
        }, 0);
    });

    // ---- smooth filter updates (falls back to normal navigation)
    var swap = function(url, push) {
        if (push === undefined) push = true;
        var box = $('#swap');
        if (!box) { location.href = url; return; }
        box.classList.add('loading');
        fetch(url).then(function(res) {
            if (!res.ok) throw new Error('load failed');
            return res.text();
        }).then(function(html) {
            var doc = new DOMParser().parseFromString(html, 'text/html'),
                nb = $('#swap', doc);
            if (!nb) { location.href = url; return; }
            box.innerHTML = nb.innerHTML;
            var sp = new URL(url, location.href).searchParams;
            $$('#filters [name]').forEach(function(i) { i.value = sp.get(i.name) || ''; });
            if (push) history.pushState({}, '', url);
            initReveal(box);
            if (window.fcRender) window.fcRender();
            box.classList.remove('loading');
        }).catch(function() { location.href = url; });
    };
    var ff = $('#filters');
    if (ff) {
        var go = function() {
            var params = new URLSearchParams(Array.prototype.slice.call(new FormData(ff).entries()).filter(function(kv) { return kv[1] !== ''; }));
            swap(location.pathname + '?' + params.toString());
        };
        on(ff, 'submit', function(e) {
            e.preventDefault();
            go();
        });
        $$('select', ff).forEach(function(s) { on(s, 'change', go); });
        var tm;
        $$('input', ff).forEach(function(i) {
            on(i, 'input', function() {
                clearTimeout(tm);
                tm = setTimeout(go, 450);
            });
        });
        addEventListener('popstate', function() { swap(location.href, false); });
    }

    // ---- password strength + confirm match
    var pwi = $('#Password'),
        mt = $('.meter i'),
        pwHint = $('#pwHint');
    if (pwi && mt) {
        on(pwi, 'input', function() {
            var v = pwi.value,
                s = 0;
            if (v.length >= 8) s++;
            if (/[a-z]/.test(v) && /[A-Z]/.test(v)) s++;
            if (/\d/.test(v)) s++;
            if (/[^A-Za-z0-9]/.test(v)) s++;
            mt.style.width = (s * 25) + '%';
            mt.dataset.s = s;
            if (pwHint) pwHint.textContent = ['Too weak', 'Weak', 'Okay', 'Good', 'Strong 💪'][s];
        });
    }
    var p2 = $('#ConfirmPassword'),
        mh = $('#matchHint');
    if (pwi && p2 && mh) {
        var m = function() {
            if (!p2.value) { mh.textContent = ''; return; }
            var ok = pwi.value === p2.value;
            mh.textContent = ok ? '✓ Passwords match' : '✗ Passwords do not match';
            mh.className = ok ? 'ok' : 'bad';
        };
        on(pwi, 'input', m);
        on(p2, 'input', m);
    }

    // ---- listing details quantity helper
    var dq = $('#qty');
    if (dq) {
        var dt = $('#total'),
            dm = $('#qmsg');
        var g = function() {
            var v = parseFloat(dq.value) || 0,
                p = +dq.dataset.price,
                mn = +dq.dataset.min,
                mx = +dq.dataset.max,
                ok = v >= mn && v <= mx;
            if (dt) dt.textContent = '₹' + inr(v * p);
            if (dm) {
                dm.textContent = v < mn ? 'Minimum order is ' + mn : (v > mx ? 'Only ' + mx + ' available' : '✓ Valid quantity');
                dm.className = 'small mt-1 ' + (ok ? 'ok' : 'bad');
            }
        };
        on(dq, 'input', g);
        g();
    }

    // ---- shared Chart.js line helper
    window.fcLine = function(cv, labels, sets, yTitle) {
        if (!window.Chart || !cv) return;
        var old = Chart.getChart(cv);
        if (old) old.destroy();
        var css = getComputedStyle(root),
            v = function(n) { return css.getPropertyValue(n).trim(); };
        var cols = [v('--p2'), v('--amber'), v('--terra'), '#4a7a8c'];
        return new Chart(cv, {
            type: 'line',
            data: {
                labels: labels,
                datasets: sets.map(function(s, i) {
                    return { label: s.label, data: s.data, borderColor: cols[i % 4], backgroundColor: cols[i % 4] + '2e', fill: sets.length === 1, tension: 0.38, pointRadius: 3, pointHoverRadius: 6, borderWidth: 3, spanGaps: true };
                })
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                interaction: { mode: 'index', intersect: false },
                animation: reduce ? false : { duration: 1500, easing: 'easeOutQuart' },
                plugins: { legend: { display: sets.length > 1, position: 'bottom', labels: { color: v('--mute') } } },
                scales: {
                    x: { grid: { display: false }, ticks: { color: v('--mute'), maxTicksLimit: 8 } },
                    y: { title: { display: !!yTitle, text: yTitle, color: v('--mute') }, grid: { color: v('--line') }, ticks: { color: v('--mute') } }
                }
            }
        });
    };

    // ---- rotating typewriter word
    var rot = $('.rot');
    if (rot) {
        var w = (rot.dataset.words || '').split(',').filter(Boolean),
            wi = 0,
            wj = 0,
            del = false;
        if (reduce || w.length < 2) { rot.textContent = w[0] || ''; } else {
            var tick = function() {
                var c = w[wi];
                rot.textContent = c.slice(0, wj);
                if (!del && wj < c.length) {
                    wj++;
                    setTimeout(tick, 90);
                } else if (!del) {
                    del = true;
                    setTimeout(tick, 1400);
                } else if (wj > 0) {
                    wj--;
                    setTimeout(tick, 45);
                } else {
                    del = false;
                    wi = (wi + 1) % w.length;
                    setTimeout(tick, 250);
                }
            };
            tick();
        }
    }

    // ---- hero parallax
    var stg = $('.stage');
    if (stg && hover && !reduce) {
        var raf;
        on(stg, 'mousemove', function(e) {
            cancelAnimationFrame(raf);
            raf = requestAnimationFrame(function() {
                var r = stg.getBoundingClientRect();
                stg.style.setProperty('--px', ((e.clientX - r.left) / r.width - 0.5) * 2);
                stg.style.setProperty('--py', ((e.clientY - r.top) / r.height - 0.5) * 2);
            });
        });
        on(stg, 'mouseleave', function() {
            stg.style.setProperty('--px', 0);
            stg.style.setProperty('--py', 0);
        });
    }

    // ---- 3D tilt
    if (hover && !reduce) {
        $$('[data-tilt]').forEach(function(el) {
            on(el, 'mousemove', function(e) {
                var r = el.getBoundingClientRect(),
                    x = (e.clientX - r.left) / r.width - 0.5,
                    y = (e.clientY - r.top) / r.height - 0.5;
                el.style.transform = 'perspective(700px) rotateY(' + (x * 8) + 'deg) rotateX(' + (-y * 8) + 'deg) translateY(-4px)';
            });
            on(el, 'mouseleave', function() { el.style.transform = ''; });
        });
    }

    // ---- tabs
    $$('[data-tabs]').forEach(function(t) {
        var tabs = $$('[role=tab]', t),
            panels = $$('[role=tabpanel]', t);
        tabs.forEach(function(b, i) {
            on(b, 'click', function() {
                tabs.forEach(function(x, k) {
                    x.setAttribute('aria-selected', String(k === i));
                    if (panels[k]) panels[k].hidden = k !== i;
                });
            });
        });
        on($('.tablist', t), 'keydown', function(e) {
            if (e.key !== 'ArrowRight' && e.key !== 'ArrowLeft') return;
            var i = tabs.findIndex(function(x) { return x.getAttribute('aria-selected') === 'true'; });
            var n = tabs[(i + (e.key === 'ArrowRight' ? 1 : -1) + tabs.length) % tabs.length];
            n.click();
            n.focus();
        });
    });

    // ---- button ripple
    document.addEventListener('pointerdown', function(e) {
        var t = e.target;
        if (!(t instanceof Element) || reduce) return;
        var b = t.closest('.btn-fc');
        if (!b) return;
        var r = b.getBoundingClientRect(),
            d = Math.max(r.width, r.height) * 2,
            s = document.createElement('span');
        s.className = 'rp';
        s.style.cssText = 'width:' + d + 'px;height:' + d + 'px;left:' + (e.clientX - r.left - d / 2) + 'px;top:' + (e.clientY - r.top - d / 2) + 'px';
        b.appendChild(s);
        setTimeout(function() { s.remove(); }, 650);
    });

    // ---- auth forms: progress, shake on server error, role text
    $$('form[data-progress]').forEach(function(f) {
        var pbar = $('.formprog', f),
            txt = $('#progTxt');
        var upd = function() {
            var ins = $$('input.form-control', f),
                role = $$('input[name=Role]', f);
            var done = ins.filter(function(i) { return i.value.trim() !== ''; }).length;
            var extra = role.length && role.some(function(r) { return r.checked; }) ? 1 : 0;
            var tot = ins.length + (role.length ? 1 : 0);
            var pct = tot > 0 ? Math.round((done + extra) / tot * 100) : 0;
            f.style.setProperty('--pg', pct + '%');
            if (pbar) pbar.style.width = pct + '%';
            if (txt) txt.textContent = pct + '% complete';
        };
        on(f, 'input', upd);
        on(f, 'change', upd);
        upd();
        if ($('.validation-summary-errors', f) || $('.field-validation-error', f)) {
            f.classList.add('shake');
            setTimeout(function() { f.classList.remove('shake'); }, 600);
        }
    });
    var desc = { Farmer: 'Farmers list produce, manage stock and orders, and track earnings.', Buyer: 'Buyers browse produce, pick a quantity and order with live stock checks.' };
    var rd = $('#roleDesc');
    if (rd) {
        $$('input[name=Role]').forEach(function(r) {
            on(r, 'change', function() {
                rd.textContent = desc[r.value] || '';
                rd.style.animation = 'none';
                void rd.offsetWidth;
                rd.style.animation = 'rise .4s both';
            });
        });
        var chk = $('input[name=Role]:checked');
        if (chk) rd.textContent = desc[chk.value] || '';
    }
})();