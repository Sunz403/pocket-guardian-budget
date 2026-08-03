(function () {
    const widget = document.getElementById('shoppingAssistant');
    if (!widget) return;
    const launcher = document.getElementById('chatLauncher'), panel = document.getElementById('chatPanel');
    const close = document.getElementById('chatClose'), messages = document.getElementById('chatMessages');
    const form = document.getElementById('chatForm'), input = document.getElementById('chatInput');
    const send = document.getElementById('chatSend'), typing = document.getElementById('chatTyping');
    const authenticated = widget.dataset.authenticated === 'true';
    let loaded = false, sending = false;

    function open() { widget.classList.add('is-open'); launcher.setAttribute('aria-expanded', 'true'); panel.setAttribute('aria-hidden', 'false'); if (!loaded) loadHistory(); setTimeout(() => input.focus(), 220); }
    function minimize() { widget.classList.remove('is-open'); launcher.setAttribute('aria-expanded', 'false'); panel.setAttribute('aria-hidden', 'true'); }
    function scrollToBottom() { messages.scrollTo({ top: messages.scrollHeight, behavior: 'smooth' }); }
    function setSendState() { send.disabled = sending || !input.value.trim(); }
    function addMessage(message, sender, timestamp) {
        const row = document.createElement('div'); row.className = `chat-message chat-message-${sender === 'User' ? 'user' : 'ai'}`;
        const bubble = document.createElement('div'); bubble.className = 'chat-bubble'; bubble.textContent = message;
        const time = document.createElement('time'); time.textContent = timestamp ? new Date(timestamp).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }) : 'Now';
        row.append(bubble, time); messages.append(row); scrollToBottom();
    }
    function addProducts(products) {
        if (!products?.length) return;
        const list = document.createElement('div'); list.className = 'chat-products';
        products.forEach(product => { const item = document.createElement('div'); item.className = 'chat-product'; const link = document.createElement('a'); link.href = '/Catalog'; link.textContent = product.name; const meta = document.createElement('span'); meta.textContent = `${product.storeName} · ${new Intl.NumberFormat('en-ZA', { style: 'currency', currency: 'ZAR', maximumFractionDigits: 0 }).format(product.price)}`; const add = document.createElement('button'); add.type = 'button'; add.dataset.productId = product.id; add.textContent = 'Add to list'; item.append(link, meta, add); list.append(item); });
        messages.append(list); scrollToBottom();
    }
    function welcome() { addMessage(authenticated ? 'Hi! I can help you understand your budget, discover products, and manage your shopping list.' : 'Hi! Log in to get budget-aware product recommendations and shopping-list help.', 'AI'); }
    async function loadHistory() {
        loaded = true;
        if (!authenticated) { welcome(); return; }
        try { const response = await fetch('/api/chat/history'); if (!response.ok) throw new Error(); const data = await response.json(); if (data.messages?.length) data.messages.forEach(m => addMessage(m.message, m.sender, m.timestamp)); else welcome(); }
        catch { addMessage('I could not load this conversation. You can still start a new one below.', 'AI'); }
    }
    async function submit(message) {
        const text = (message || input.value).trim(); if (!text || sending) return;
        if (!authenticated) { window.location.href = '/Account/Login?returnUrl=' + encodeURIComponent(window.location.pathname); return; }
        sending = true; setSendState(); input.value = ''; addMessage(text, 'User'); typing.hidden = false; scrollToBottom();
        try { const response = await fetch('/api/chat/send', { method: 'POST', headers: { 'Content-Type': 'application/json', 'Accept': 'application/json' }, body: JSON.stringify({ message: text }) }); const data = await readJson(response); if (!response.ok) throw new Error(data.message || 'I could not send that message.'); addMessage(data.aiMessage.message, 'AI', data.aiMessage.timestamp); addProducts(data.products); }
        catch (error) { addMessage(error.message || 'I could not respond just now. Please try again.', 'AI'); }
        finally { typing.hidden = true; sending = false; setSendState(); input.focus(); }
    }
    launcher.addEventListener('click', open); close.addEventListener('click', minimize);
    input.addEventListener('input', setSendState); form.addEventListener('submit', event => { event.preventDefault(); submit(); });
    widget.querySelectorAll('[data-chat-prompt]').forEach(button => button.addEventListener('click', () => submit(button.dataset.chatPrompt)));
    async function readJson(response) { const body = await response.text(); try { return body ? JSON.parse(body) : {}; } catch { return { message: response.ok ? 'The assistant returned an unexpected response.' : 'The AI assistant cannot reach its data source right now. Please try again shortly.' }; } }
    messages.addEventListener('click', async event => { const button = event.target.closest('[data-product-id]'); if (!button || !authenticated) return; button.disabled = true; try { const response = await fetch(`/ShoppingList/Add/${button.dataset.productId}`, { method: 'POST', headers: { Accept: 'application/json' } }); const data = await readJson(response); if (!response.ok) throw new Error(data.message || 'Could not add that product.'); button.textContent = 'Added'; } catch (error) { button.disabled = false; addMessage(error.message || 'Could not update your shopping list.', 'AI'); } });
})();
