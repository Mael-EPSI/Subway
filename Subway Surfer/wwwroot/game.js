/* ============================================================
   SUBWAY SURFER — Thin Renderer
   All game logic runs server-side in C#.
   This file ONLY: connects via WebSocket, draws frames, sends input.
   ============================================================ */

// ======================== CONFIG (rendering only) ========================
const CFG = {
    WIDTH: 800, HEIGHT: 600, FOV: 250, VP_Y_RATIO: 0.35,
    LANE_W: 110, TRACK_HW: 180,
    T_W: 86, T_H: 115,
    C_R: 14, C_FLOAT_H: 40,
    JUMP_H: 130,
    MAX_Z: 3000, TIE_SP: 180,
};

let canvas, ctx, vpX, vpY;

// Latest state snapshot from server
let S = null;

// Pre-generated stars
const stars = Array.from({length: 60}, () => ({
    x: Math.random(), y: Math.random(),
    s: Math.random() * 1.5 + 0.5,
    b: Math.random() * 0.5 + 0.3,
}));

// ======================== AUDIO ========================
let audioCtx = null;

function ensureAudio() {
    if (!audioCtx) audioCtx = new (window.AudioContext || window.webkitAudioContext)();
    if (audioCtx.state === 'suspended') audioCtx.resume();
}

function playSound(type) {
    if (!audioCtx) return;
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

// ======================== WEBSOCKET ========================
let ws = null;

function connectWS() {
    const proto = location.protocol === 'https:' ? 'wss:' : 'ws:';
    ws = new WebSocket(`${proto}//${location.host}/ws`);
    ws.onmessage = e => {
        S = JSON.parse(e.data);
        if (S.sounds) {
            ensureAudio();
            for (const snd of S.sounds) playSound(snd);
        }
    };
    ws.onclose = () => setTimeout(connectWS, 1000);
    ws.onerror = () => ws.close();
}

function send(action) {
    if (ws && ws.readyState === WebSocket.OPEN) ws.send(action);
}

// ======================== PERSPECTIVE ========================
function project(wx, wz) {
    if (wz <= 0) wz = 0.001;
    const s = CFG.FOV / (CFG.FOV + wz);
    return { x: vpX + wx * s, y: vpY + (canvas.height - vpY) * s, s };
}

function depthAlpha(z) {
    if (z > 2000) return 0.25;
    return 0.25 + 0.75 * (1 - z / 2000);
}

// ======================== INPUT ========================
let touchSX = 0, touchSY = 0;
const keysDown = {};

function setupInput() {
    window.addEventListener('keydown', e => {
        if (['ArrowUp','ArrowDown','ArrowLeft','ArrowRight','Space'].includes(e.code))
            e.preventDefault();
        if (keysDown[e.code]) return;
        keysDown[e.code] = true;
        ensureAudio();
        onAction(e.code);
    });
    window.addEventListener('keyup', e => { keysDown[e.code] = false; });

    canvas.addEventListener('touchstart', e => {
        e.preventDefault();
        touchSX = e.touches[0].clientX;
        touchSY = e.touches[0].clientY;
    }, {passive: false});

    canvas.addEventListener('touchend', e => {
        e.preventDefault();
        ensureAudio();
        const dx = e.changedTouches[0].clientX - touchSX;
        const dy = e.changedTouches[0].clientY - touchSY;
        if (Math.abs(dx) > Math.abs(dy) && Math.abs(dx) > 30) {
            send(dx > 0 ? 'right' : 'left');
        } else if (dy < -30) {
            send('jump');
        } else {
            if (S && S.phase === 'menu') send('start');
            else if (S && S.phase === 'gameover') send('restart');
        }
    }, {passive: false});
}

function onAction(code) {
    if (!S) return;
    if (S.phase === 'playing') {
        if (code === 'ArrowLeft' || code === 'KeyA') send('left');
        if (code === 'ArrowRight' || code === 'KeyD') send('right');
        if (code === 'ArrowUp' || code === 'KeyW' || code === 'Space') send('jump');
    }
    if (S.phase === 'menu' && (code === 'Space' || code === 'Enter')) send('start');
    if (S.phase === 'gameover' && (code === 'Space' || code === 'Enter')) send('restart');
}

// ======================== RENDERING ========================
function render() {
    requestAnimationFrame(render);
    if (!S) {
        ctx.fillStyle = '#000';
        ctx.fillRect(0, 0, canvas.width, canvas.height);
        ctx.fillStyle = '#888';
        ctx.font = '20px sans-serif';
        ctx.textAlign = 'center';
        ctx.fillText('Connecting...', vpX, canvas.height / 2);
        return;
    }

    ctx.clearRect(0, 0, canvas.width, canvas.height);
    ctx.save();

    if (S.shakeT > 0) {
        const m = S.shakeT * 25;
        ctx.translate((Math.random() - 0.5) * m, (Math.random() - 0.5) * m);
    }

    drawSky();
    drawWalls();
    drawTrack();
    drawTrackTies();
    drawLaneLines();
    drawWallWindows();
    drawObjects();
    drawPlayer();
    drawParticles();
    drawFlash();

    ctx.restore();
    drawHUD();
    if (S.phase === 'menu')     drawMenu();
    if (S.phase === 'gameover') drawGameOver();
}

// ---- SKY ----
function drawSky() {
    const grad = ctx.createLinearGradient(0, 0, 0, vpY);
    grad.addColorStop(0, '#06061a');
    grad.addColorStop(1, '#141432');
    ctx.fillStyle = grad;
    ctx.fillRect(0, 0, canvas.width, vpY + 1);
    for (const st of stars) {
        ctx.fillStyle = `rgba(255,255,255,${st.b})`;
        ctx.fillRect(st.x * canvas.width, st.y * vpY * 0.9, st.s, st.s);
    }
    const glow = ctx.createRadialGradient(vpX, vpY, 0, vpX, vpY, 200);
    glow.addColorStop(0, 'rgba(60,50,120,0.35)');
    glow.addColorStop(1, 'rgba(60,50,120,0)');
    ctx.fillStyle = glow;
    ctx.fillRect(vpX - 200, vpY - 100, 400, 200);
}

// ---- WALLS ----
function drawWalls() {
    const grad = ctx.createLinearGradient(0, vpY, 0, canvas.height);
    grad.addColorStop(0, '#110f24');
    grad.addColorStop(1, '#1a1840');
    ctx.fillStyle = grad;
    ctx.fillRect(0, vpY, canvas.width, canvas.height - vpY);
}

// ---- TRACK ----
function drawTrack() {
    const hw = CFG.TRACK_HW;
    const farL = project(-hw, CFG.MAX_Z);
    const farR = project( hw, CFG.MAX_Z);
    const nearL = project(-hw, 1);
    const nearR = project( hw, 1);
    const grad = ctx.createLinearGradient(0, vpY, 0, canvas.height);
    grad.addColorStop(0, '#222');
    grad.addColorStop(1, '#333');
    ctx.fillStyle = grad;
    ctx.beginPath();
    ctx.moveTo(farL.x, farL.y);
    ctx.lineTo(farR.x, farR.y);
    ctx.lineTo(nearR.x, Math.min(nearR.y, canvas.height));
    ctx.lineTo(nearL.x, Math.min(nearL.y, canvas.height));
    ctx.closePath();
    ctx.fill();
    ctx.strokeStyle = '#555'; ctx.lineWidth = 1;
    drawEdgeLine(-hw);
    drawEdgeLine( hw);
}

function drawEdgeLine(wx) {
    ctx.beginPath();
    let first = true;
    for (let z = 1; z < CFG.MAX_Z; z += (z < 200 ? 10 : 50)) {
        const p = project(wx, z);
        if (first) { ctx.moveTo(p.x, p.y); first = false; }
        else ctx.lineTo(p.x, p.y);
    }
    ctx.stroke();
}

// ---- TRACK TIES ----
function drawTrackTies() {
    const sp = CFG.TIE_SP;
    const scroll = S.scroll || 0;
    const off = (sp - scroll % sp) % sp;
    ctx.lineCap = 'butt';
    for (let z = off; z < CFG.MAX_Z; z += sp) {
        if (z <= 1) continue;
        const left  = project(-CFG.TRACK_HW * 0.92, z);
        const right = project( CFG.TRACK_HW * 0.92, z);
        const a = depthAlpha(z);
        ctx.strokeStyle = `rgba(80,65,45,${a})`;
        ctx.lineWidth = Math.max(1, 4 * left.s);
        ctx.beginPath();
        ctx.moveTo(left.x, left.y);
        ctx.lineTo(right.x, right.y);
        ctx.stroke();
    }
}

// ---- LANE LINES ----
function drawLaneLines() {
    const dashLen = 80, gap = 80, period = dashLen + gap;
    const scroll = S.scroll || 0;
    for (let d = -0.5; d <= 0.5; d += 1) {
        const wx = d * CFG.LANE_W;
        const off = (period - scroll % period) % period;
        for (let z = off; z < CFG.MAX_Z; z += period) {
            if (z <= 1) continue;
            const z2 = z + dashLen;
            const p1 = project(wx, z);
            const p2 = project(wx, z2);
            const a = depthAlpha(z);
            ctx.strokeStyle = `rgba(255,200,0,${a * 0.6})`;
            ctx.lineWidth = Math.max(1, 3 * p1.s);
            ctx.beginPath();
            ctx.moveTo(p1.x, p1.y);
            ctx.lineTo(p2.x, p2.y);
            ctx.stroke();
        }
    }
}

// ---- WALL WINDOWS ----
function drawWallWindows() {
    const buildingSp = 300;
    const scroll = S.scroll || 0;
    const off = (buildingSp - scroll % buildingSp) % buildingSp;
    const hw = CFG.TRACK_HW;
    for (let z = off + 10; z < 2200; z += buildingSp) {
        if (z <= 2) continue;
        const p = project(0, z);
        const a = depthAlpha(z);
        const leftEdge  = project(-hw, z);
        const rightEdge = project( hw, z);
        const ws = Math.max(2, 14 * p.s);
        if (ws < 2) continue;
        for (let col = 1; col <= 3; col++) {
            for (let row = 0; row < 4; row++) {
                const lit = ((Math.floor(z / buildingSp) * 7 + col * 3 + row * 11) % 5) > 1;
                const c = lit ? `rgba(255,200,100,${a * 0.55})` : `rgba(35,30,55,${a * 0.4})`;
                ctx.fillStyle = c;
                const lx = leftEdge.x - ws * col * 1.8 - ws;
                const ly = leftEdge.y - ws * (row + 1) * 1.6;
                if (lx > 0 && ly > vpY) ctx.fillRect(lx, ly, ws, ws * 1.2);
                const rx = rightEdge.x + ws * (col - 1) * 1.8 + ws * 0.5;
                const ry = rightEdge.y - ws * (row + 1) * 1.6;
                if (rx < canvas.width && ry > vpY) ctx.fillRect(rx, ry, ws, ws * 1.2);
            }
        }
    }
}

// ---- GAME OBJECTS (sorted by Z, back-to-front) ----
function drawObjects() {
    const all = [];
    for (const t of S.trains) all.push({type:'train', z: t.z, ref: t});
    for (const c of S.coins)  all.push({type:'coin',  z: c.z, ref: c});
    all.sort((a, b) => b.z - a.z);
    for (const o of all) {
        if (o.type === 'train') drawTrain(o.ref);
        else drawCoin(o.ref);
    }
}

// ---- TRAIN ----
function drawTrain(t) {
    const wx = t.lane * CFG.LANE_W;
    const p = project(wx, t.z);
    const w = CFG.T_W * p.s;
    const h = CFG.T_H * p.s;
    if (w < 2 || p.y < vpY - 5) return;

    const x = p.x - w / 2;
    const y = p.y - h;
    const a = depthAlpha(t.z);
    ctx.globalAlpha = a;

    // Body
    ctx.fillStyle = t.color;
    roundRect(x, y, w, h, Math.max(2, 6 * p.s));
    ctx.strokeStyle = 'rgba(0,0,0,0.5)';
    ctx.lineWidth = Math.max(1, 2 * p.s);
    roundRectStroke(x, y, w, h, Math.max(2, 6 * p.s));

    // Windshield
    const wsM = w * 0.10;
    const wsY = y + h * 0.06;
    const wsH = h * 0.28;
    ctx.fillStyle = '#1a3a5e';
    ctx.fillRect(x + wsM, wsY, w - wsM * 2, wsH);
    ctx.fillStyle = 'rgba(120,170,220,0.25)';
    ctx.fillRect(x + wsM + w * 0.05, wsY + wsH * 0.15, w * 0.35, wsH * 0.4);

    // Headlights
    if (w > 12) {
        const lr = Math.max(2, w * 0.065);
        const hly = y + h * 0.60;
        ctx.fillStyle = '#ffee88';
        ctx.beginPath(); ctx.arc(x + w * 0.22, hly, lr, 0, Math.PI * 2); ctx.fill();
        ctx.fillStyle = 'rgba(255,238,136,0.25)';
        ctx.beginPath(); ctx.arc(x + w * 0.22, hly, lr * 2.2, 0, Math.PI * 2); ctx.fill();
        ctx.fillStyle = '#ffee88';
        ctx.beginPath(); ctx.arc(x + w * 0.78, hly, lr, 0, Math.PI * 2); ctx.fill();
        ctx.fillStyle = 'rgba(255,238,136,0.25)';
        ctx.beginPath(); ctx.arc(x + w * 0.78, hly, lr * 2.2, 0, Math.PI * 2); ctx.fill();
    }

    // Grill lines
    if (w > 18) {
        ctx.strokeStyle = 'rgba(0,0,0,0.4)';
        ctx.lineWidth = Math.max(1, 1.5 * p.s);
        for (let i = 0; i < 3; i++) {
            const gy = y + h * (0.73 + i * 0.055);
            ctx.beginPath(); ctx.moveTo(x + w * 0.18, gy); ctx.lineTo(x + w * 0.82, gy); ctx.stroke();
        }
    }

    // Roof rail
    if (w > 20) {
        ctx.fillStyle = '#1a1a1a';
        ctx.fillRect(x + w * 0.28, y + h * 0.015, w * 0.44, h * 0.04);
    }

    // Bumper
    ctx.fillStyle = '#555';
    ctx.fillRect(x + w * 0.06, y + h * 0.93, w * 0.88, h * 0.045);
    ctx.globalAlpha = 1;
}

function roundRect(x, y, w, h, r) {
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
    ctx.closePath(); ctx.fill();
}

function roundRectStroke(x, y, w, h, r) {
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
    ctx.closePath(); ctx.stroke();
}

// ---- COIN ----
function drawCoin(c) {
    const wx = c.lane * CFG.LANE_W;
    const p = project(wx, c.z);
    const r = CFG.C_R * p.s;
    if (r < 1) return;
    const floatY = CFG.C_FLOAT_H * p.s;
    const bob = Math.sin(c.bob) * 4 * p.s;
    const cy = p.y - floatY + bob;
    const a = depthAlpha(c.z);
    ctx.globalAlpha = a;

    // Shadow
    ctx.fillStyle = 'rgba(0,0,0,0.2)';
    ctx.beginPath(); ctx.ellipse(p.x, p.y - 2 * p.s, r * 0.8, r * 0.3, 0, 0, Math.PI * 2); ctx.fill();

    // Coin body
    ctx.fillStyle = '#ffd700';
    ctx.beginPath(); ctx.arc(p.x, cy, r, 0, Math.PI * 2); ctx.fill();
    ctx.fillStyle = '#ffed4a';
    ctx.beginPath(); ctx.arc(p.x, cy, r * 0.6, 0, Math.PI * 2); ctx.fill();

    // Dollar sign
    if (r > 5) {
        ctx.fillStyle = '#b8860b';
        ctx.font = `bold ${Math.max(8, Math.round(r * 1.2))}px sans-serif`;
        ctx.textAlign = 'center'; ctx.textBaseline = 'middle';
        ctx.fillText('$', p.x, cy + 1);
    }
    ctx.globalAlpha = 1;
}

// ---- PLAYER ----
function drawPlayer() {
    if (!S.player) return;
    const pl = S.player;
    const px = vpX + pl.vx * (CFG.FOV / (CFG.FOV + 50));
    const groundY = project(0, 50).y;
    const jumpOff = pl.jumpY * (CFG.FOV / (CFG.FOV + 50));
    const py = groundY - jumpOff;

    const sc = 1.0;
    const headR = 12 * sc;
    const bodyH = 30 * sc;
    const legL  = 20 * sc;
    const armL  = 18 * sc;

    // Shadow
    ctx.fillStyle = 'rgba(0,0,0,0.25)';
    ctx.beginPath();
    ctx.ellipse(px, groundY + 2, 16 * sc, 5 * sc, 0, 0, Math.PI * 2);
    ctx.fill();

    const baseY = py;
    const legSwing = Math.sin(pl.leg) * 12;

    // Left leg
    ctx.strokeStyle = '#1565c0';
    ctx.lineWidth = 5 * sc;
    ctx.lineCap = 'round';
    ctx.beginPath();
    ctx.moveTo(px - 5 * sc, baseY);
    ctx.lineTo(px - 5 * sc + legSwing * 0.4, baseY + legL);
    ctx.stroke();

    // Right leg
    ctx.beginPath();
    ctx.moveTo(px + 5 * sc, baseY);
    ctx.lineTo(px + 5 * sc - legSwing * 0.4, baseY + legL);
    ctx.stroke();

    // Body
    ctx.strokeStyle = '#e53935';
    ctx.lineWidth = 8 * sc;
    ctx.beginPath();
    ctx.moveTo(px, baseY - bodyH);
    ctx.lineTo(px, baseY);
    ctx.stroke();

    // Left arm
    ctx.strokeStyle = '#e53935';
    ctx.lineWidth = 4 * sc;
    ctx.beginPath();
    ctx.moveTo(px, baseY - bodyH * 0.7);
    ctx.lineTo(px - armL * 0.7, baseY - bodyH * 0.4 - legSwing * 0.3);
    ctx.stroke();

    // Right arm
    ctx.beginPath();
    ctx.moveTo(px, baseY - bodyH * 0.7);
    ctx.lineTo(px + armL * 0.7, baseY - bodyH * 0.4 + legSwing * 0.3);
    ctx.stroke();

    // Head
    ctx.fillStyle = '#ffcc80';
    ctx.beginPath();
    ctx.arc(px, baseY - bodyH - headR, headR, 0, Math.PI * 2);
    ctx.fill();
    ctx.strokeStyle = '#5d4037';
    ctx.lineWidth = 2;
    ctx.stroke();

    // Cap
    ctx.fillStyle = '#e53935';
    ctx.beginPath();
    ctx.ellipse(px, baseY - bodyH - headR * 1.5, headR * 1.2, headR * 0.45, 0, Math.PI, 0);
    ctx.fill();
    ctx.fillRect(px - headR * 1.3, baseY - bodyH - headR * 1.55, headR * 2.6, headR * 0.22);

    // Eyes
    ctx.fillStyle = '#333';
    ctx.beginPath();
    ctx.arc(px - 4, baseY - bodyH - headR - 1, 1.8, 0, Math.PI * 2); ctx.fill();
    ctx.beginPath();
    ctx.arc(px + 4, baseY - bodyH - headR - 1, 1.8, 0, Math.PI * 2); ctx.fill();
}

// ---- PARTICLES ----
function drawParticles() {
    for (const p of S.particles) {
        const alpha = Math.max(0, p.life / p.maxLife);
        ctx.fillStyle = p.color;
        ctx.globalAlpha = alpha;
        ctx.beginPath();
        ctx.arc(p.x, p.y, p.r, 0, Math.PI * 2);
        ctx.fill();
    }
    ctx.globalAlpha = 1;
}

// ---- FLASH ----
function drawFlash() {
    if (S.flashA > 0) {
        ctx.fillStyle = `rgba(255,255,255,${S.flashA * 0.7})`;
        ctx.fillRect(0, 0, canvas.width, canvas.height);
    }
}

// ---- HUD ----
function drawHUD() {
    ctx.fillStyle = '#fff';
    ctx.font = 'bold 22px monospace';
    ctx.textAlign = 'left';
    ctx.fillText(`Score: ${S.score}`, 18, 35);
    if (S.highScore > 0) {
        ctx.font = '15px monospace';
        ctx.fillText(`Best: ${S.highScore}`, 18, 56);
    }
    ctx.font = '13px monospace';
    ctx.fillStyle = '#aaa';
    ctx.textAlign = 'right';
    ctx.fillText(`Speed: ${Math.round(S.speed)}`, canvas.width - 18, 35);
}

// ---- MENU ----
function drawMenu() {
    ctx.fillStyle = 'rgba(0,0,0,0.55)';
    ctx.fillRect(0, 0, canvas.width, canvas.height);
    ctx.fillStyle = '#ffd700';
    ctx.font = 'bold 44px sans-serif';
    ctx.textAlign = 'center';
    ctx.fillText('SUBWAY SURFER', vpX, canvas.height * 0.33);
    ctx.fillStyle = '#fff';
    ctx.font = '20px sans-serif';
    ctx.fillText('Press SPACE or tap to start', vpX, canvas.height * 0.50);
    ctx.fillStyle = '#aaa';
    ctx.font = '15px sans-serif';
    ctx.fillText('← → to move  |  ↑ / Space to jump', vpX, canvas.height * 0.60);
}

// ---- GAME OVER ----
function drawGameOver() {
    ctx.fillStyle = 'rgba(0,0,0,0.6)';
    ctx.fillRect(0, 0, canvas.width, canvas.height);
    ctx.fillStyle = '#e53935';
    ctx.font = 'bold 42px sans-serif';
    ctx.textAlign = 'center';
    ctx.fillText('GAME OVER', vpX, canvas.height * 0.33);
    ctx.fillStyle = '#ffd700';
    ctx.font = 'bold 28px monospace';
    ctx.fillText(`Score: ${S.score}`, vpX, canvas.height * 0.45);
    if (S.highScore > 0) {
        ctx.fillStyle = '#fff';
        ctx.font = '18px monospace';
        ctx.fillText(`Best: ${S.highScore}`, vpX, canvas.height * 0.53);
    }
    ctx.fillStyle = '#fff';
    ctx.font = '18px sans-serif';
    ctx.fillText('Press SPACE or tap to restart', vpX, canvas.height * 0.65);
}

// ======================== INIT ========================
function resizeCanvas() {
    const dpr = window.devicePixelRatio || 1;
    const maxW = CFG.WIDTH, maxH = CFG.HEIGHT;
    const aspect = maxW / maxH;
    let w = window.innerWidth, h = window.innerHeight;
    if (w / h > aspect) w = h * aspect;
    else h = w / aspect;
    canvas.style.width  = Math.floor(w) + 'px';
    canvas.style.height = Math.floor(h) + 'px';
    canvas.width  = Math.floor(w * dpr);
    canvas.height = Math.floor(h * dpr);
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    canvas.width  = maxW;
    canvas.height = maxH;
    vpX = canvas.width / 2;
    vpY = canvas.height * CFG.VP_Y_RATIO;
}

window.addEventListener('load', () => {
    canvas = document.getElementById('gameCanvas');
    ctx = canvas.getContext('2d');
    resizeCanvas();
    window.addEventListener('resize', resizeCanvas);
    setupInput();
    connectWS();
    requestAnimationFrame(render);
});
