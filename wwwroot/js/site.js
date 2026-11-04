(() => {
    const nav = document.querySelector('.fc-nav');
    addEventListener('scroll', () => nav && nav.classList.toggle('scrolled', scrollY > 8), { passive: true });

    // scroll reveal with stagger
    const io = new IntersectionObserver(es => es.forEach(e => {
        if (e.isIntersecting) {
            e.target.classList.add('in');
            io.unobserve(e.target);
        }
    }), { threshold: .12 });
    document.querySelectorAll('.reveal').forEach((el, i) => {
        el.style.transitionDelay = (i % 4) * 80 + 'ms';
        io.observe(el);
    });

    // live order total + client-side hint (server re-validates everything)
    const q = document.getElementById('qty');
    if (q) {
        const t = document.getElementById('total'),
            m = document.getElementById('qmsg');
        const f = () => {
            const v = parseFloat(q.value) || 0,
                p = +q.dataset.price,
                mn = +q.dataset.min,
                mx = +q.dataset.max;
            t.textContent = '₹' + (v * p).toLocaleString('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
            const ok = v >= mn && v <= mx;
            m.textContent = v < mn ? `Minimum order is ${mn}` : v > mx ? `Only ${mx} available` : '✓ Valid quantity';
            m.className = 'small mt-1 ' + (ok ? 'ok' : 'bad');
        };
        q.addEventListener('input', f);
        f();
    }
})();