/* ============================================================
   Canvas2D + Audio interop bridge for Blazor WASM.
   Minimal JS — all rendering logic is orchestrated from C#.
   ============================================================ */
window.GameInterop = (() => {
    let canvas, ctx, vpX, vpY;
    let audioCtx = null;
    const W = 800, H = 600;

    function init() {
        canvas = document.getElementById('gameCanvas');
        ctx = canvas.getContext('2d');
        canvas.width = W;
        canvas.height = H;
        vpX = W / 2;
        vpY = Math.floor(H * 0.35);
        resize();
        window.addEventListener('resize', resize);
    }

    function resize() {
        const ratio = W / H;
        let w = window.innerWidth, h = window.innerHeight;
        if (w / h > ratio) w = h * ratio; else h = w / ratio;
        canvas.style.width = Math.floor(w) + 'px';
        canvas.style.height = Math.floor(h) + 'px';
    }

    // ─── Drawing primitives ─────────────────────────
    function clear() { ctx.clearRect(0, 0, W, H); }
    function save() { ctx.save(); }
    function restore() { ctx.restore(); }

    function translate(x, y) { ctx.translate(x, y); }
    function setAlpha(a) { ctx.globalAlpha = a; }

    function fillRect(x, y, w, h, color) {
        ctx.fillStyle = color;
        ctx.fillRect(x, y, w, h);
    }

    function strokeRect(x, y, w, h, color, lw) {
        ctx.strokeStyle = color;
        ctx.lineWidth = lw;
        ctx.strokeRect(x, y, w, h);
    }

    function line(x1, y1, x2, y2, color, lw) {
        ctx.strokeStyle = color;
        ctx.lineWidth = lw;
        ctx.beginPath();
        ctx.moveTo(x1, y1);
        ctx.lineTo(x2, y2);
        ctx.stroke();
    }

    function fillCircle(x, y, r, color) {
        ctx.fillStyle = color;
        ctx.beginPath();
        ctx.arc(x, y, r, 0, Math.PI * 2);
        ctx.fill();
    }

    function fillEllipse(x, y, rx, ry, color) {
        ctx.fillStyle = color;
        ctx.beginPath();
        ctx.ellipse(x, y, rx, ry, 0, 0, Math.PI * 2);
        ctx.fill();
    }

    function fillEllipseArc(x, y, rx, ry, startAngle, endAngle, color) {
        ctx.fillStyle = color;
        ctx.beginPath();
        ctx.ellipse(x, y, rx, ry, 0, startAngle, endAngle);
        ctx.fill();
    }

    function fillText(text, x, y, color, font, align, baseline) {
        ctx.fillStyle = color;
        ctx.font = font;
        ctx.textAlign = align || 'center';
        ctx.textBaseline = baseline || 'middle';
        ctx.fillText(text, x, y);
    }

    function linGrad(x0, y0, x1, y1, stops) {
        const g = ctx.createLinearGradient(x0, y0, x1, y1);
        for (let i = 0; i < stops.length; i += 2)
            g.addColorStop(stops[i], stops[i + 1]);
        ctx.fillStyle = g;
    }

    function radGrad(x, y, r0, r1, stops) {
        const g = ctx.createRadialGradient(x, y, r0, x, y, r1);
        for (let i = 0; i < stops.length; i += 2)
            g.addColorStop(stops[i], stops[i + 1]);
        ctx.fillStyle = g;
    }

    function fillGradRect(x, y, w, h) {
        ctx.fillRect(x, y, w, h);
    }

    function polyFill(pts) {
        ctx.beginPath();
        ctx.moveTo(pts[0], pts[1]);
        for (let i = 2; i < pts.length; i += 2)
            ctx.lineTo(pts[i], pts[i + 1]);
        ctx.closePath();
        ctx.fill();
    }

    function polyLine(pts, color, lw) {
        ctx.strokeStyle = color;
        ctx.lineWidth = lw;
        ctx.beginPath();
        ctx.moveTo(pts[0], pts[1]);
        for (let i = 2; i < pts.length; i += 2)
            ctx.lineTo(pts[i], pts[i + 1]);
        ctx.stroke();
    }

    function setLineCap(cap) { ctx.lineCap = cap; }

    function roundRect(x, y, w, h, r, color, doFill, doStroke, strokeColor, strokeW) {
        r = Math.min(r, w / 2, h / 2);
        ctx.beginPath();
        ctx.moveTo(x + r, y); ctx.lineTo(x + w - r, y);
        ctx.quadraticCurveTo(x + w, y, x + w, y + r);
        ctx.lineTo(x + w, y + h - r);
        ctx.quadraticCurveTo(x + w, y + h, x + w - r, y + h);
        ctx.lineTo(x + r, y + h);
        ctx.quadraticCurveTo(x, y + h, x, y + h - r);
        ctx.lineTo(x, y + r);
        ctx.quadraticCurveTo(x, y, x + r, y);
        ctx.closePath();
        if (doFill) { ctx.fillStyle = color; ctx.fill(); }
        if (doStroke) { ctx.strokeStyle = strokeColor; ctx.lineWidth = strokeW; ctx.stroke(); }
    }

    function now() { return performance.now(); }

    // ─── Audio ──────────────────────────────────────
    function ensureAudio() {
        if (!audioCtx) audioCtx = new (window.AudioContext || window.webkitAudioContext)();
        if (audioCtx.state === 'suspended') audioCtx.resume();
    }

    function playSound(type) {
        ensureAudio();
        const osc = audioCtx.createOscillator();
        const g = audioCtx.createGain();
        osc.connect(g); g.connect(audioCtx.destination);
        const t = audioCtx.currentTime;
        if (type === 'coin') {
            osc.type = 'sine';
            osc.frequency.setValueAtTime(880, t);
            osc.frequency.linearRampToValueAtTime(1320, t + 0.10);
            g.gain.setValueAtTime(0.18, t);
            g.gain.linearRampToValueAtTime(0, t + 0.15);
            osc.start(t); osc.stop(t + 0.15);
        } else if (type === 'crash') {
            osc.type = 'sawtooth';
            osc.frequency.setValueAtTime(200, t);
            osc.frequency.linearRampToValueAtTime(40, t + 0.35);
            g.gain.setValueAtTime(0.28, t);
            g.gain.linearRampToValueAtTime(0, t + 0.40);
            osc.start(t); osc.stop(t + 0.40);
        } else if (type === 'jump') {
            osc.type = 'sine';
            osc.frequency.setValueAtTime(330, t);
            osc.frequency.linearRampToValueAtTime(660, t + 0.12);
            g.gain.setValueAtTime(0.12, t);
            g.gain.linearRampToValueAtTime(0, t + 0.18);
            osc.start(t); osc.stop(t + 0.18);
        } else if (type === 'lane') {
            osc.type = 'sine';
            osc.frequency.setValueAtTime(520, t);
            g.gain.setValueAtTime(0.08, t);
            g.gain.linearRampToValueAtTime(0, t + 0.05);
            osc.start(t); osc.stop(t + 0.05);
        }
    }

    return {
        init, clear, save, restore, translate, setAlpha,
        fillRect, strokeRect, line, fillCircle, fillEllipse, fillEllipseArc,
        fillText, linGrad, radGrad, fillGradRect, polyFill, polyLine,
        setLineCap, roundRect, now, playSound, ensureAudio
    };
})();
