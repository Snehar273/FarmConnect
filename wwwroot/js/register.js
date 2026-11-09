(function() {
    var f = document.querySelector('.regform');
    if (!f) return;
    var $ = function(s) { return f.querySelector(s); };
    var $$ = function(s) { return Array.prototype.slice.call(f.querySelectorAll(s)); };
    var id = function(x) { return document.getElementById(x); };
    var val = function(x) { var e = id(x); return e ? e.value.trim() : ''; };
    var roleOf = function() { var c = $('input[name=Role]:checked'); return c ? c.value : ''; };

    // mobile: digits only
    var mob = id('Mobile');
    if (mob) mob.addEventListener('input', function() {
        var d = mob.value.replace(/\D/g, '').slice(0, 10);
        if (d !== mob.value) { mob.value = d;
            mob.dispatchEvent(new Event('blur')); }
    });

    // city quick-picks
    $$('[data-city]').forEach(function(b) {
        b.addEventListener('click', function() {
            var c = id('Location');
            if (!c) return;
            c.value = b.getAttribute('data-city');
            c.dispatchEvent(new Event('input', { bubbles: true }));
            $$('[data-city]').forEach(function(x) { x.classList.toggle('on', x === b); });
        });
    });

    // password generator (browser crypto; nothing is sent anywhere until you submit)
    var gen = $('[data-gen]');
    if (gen) gen.addEventListener('click', function() {
        var sets = ['ABCDEFGHJKLMNPQRSTUVWXYZ', 'abcdefghijkmnopqrstuvwxyz', '23456789', '!@#$%^&*-_+='];
        var all = sets.join('');
        var rnd = function(n) { var a = new Uint32Array(1);
            window.crypto.getRandomValues(a); return a[0] % n; };
        var out = sets.map(function(s) { return s.charAt(rnd(s.length)); });
        while (out.length < 14) out.push(all.charAt(rnd(all.length)));
        for (var j = out.length - 1; j > 0; j--) { var k = rnd(j + 1),
                t = out[j];
            out[j] = out[k];
            out[k] = t; }
        var pw = out.join(''),
            p1 = id('Password'),
            p2 = id('ConfirmPassword');
        p1.type = 'text';
        p1.value = pw;
        p2.value = pw;
        [p1, p2].forEach(function(e) { e.dispatchEvent(new Event('input', { bubbles: true })); });
        var eye = p1.closest('.ff').querySelector('[data-pw]');
        if (eye) { eye.textContent = '🙈';
            eye.setAttribute('aria-pressed', 'true'); }
        gen.textContent = '✓ Generated. Save it in a password manager';
    });

    // live review card + checklist
    var setPv = function(key, text) { $$('[data-pv="' + key + '"]').forEach(function(e) { e.textContent = text; }); };
    var upd = function() {
        var n = val('FullName'),
            e = val('Email'),
            m = val('Mobile'),
            c = val('Location'),
            r = roleOf();
        var pw = id('Password') ? id('Password').value : '',
            cf = id('ConfirmPassword') ? id('ConfirmPassword').value : '';
        setPv('name', n || 'Your name');
        setPv('email', e || '—');
        setPv('mobile', m ? '+91 ' + m : '—');
        setPv('city', c || 'Not set');
        setPv('role', r === 'Farmer' ? '🧑‍🌾 Farmer' : (r === 'Buyer' ? '🛒 Buyer' : '—'));
        setPv('avatar', n ? n.charAt(0).toUpperCase() : '?');
        f.setAttribute('data-role', r);
        var strong = pw.length >= 8 && /[a-z]/.test(pw) && /[A-Z]/.test(pw) && /\d/.test(pw) && /[^A-Za-z0-9]/.test(pw);
        var chk = { role: !!r, name: n.length > 1, email: /^[^@\s]+@[^@\s]+\.[^@\s]+$/.test(e), mobile: /^[6-9]\d{9}$/.test(m), password: strong && pw === cf };
        $$('[data-chk]').forEach(function(li) { li.classList.toggle('ok', !!chk[li.getAttribute('data-chk')]); });
        var all = Object.keys(chk).every(function(k) { return chk[k]; });
        var note = $('.ready-note');
        if (note) note.textContent = all ? 'Everything looks good. You are ready to create your account.' : 'Complete the unchecked items to continue.';
        f.classList.toggle('allok', all);
    };
    f.addEventListener('input', upd);
    f.addEventListener('change', upd);
    upd();
})();