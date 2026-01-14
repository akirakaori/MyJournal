// wwwroot/js/fullcalendar-interop.js
window.fullCalendarInterop = (() => {
    const calendars = new Map(); // elementId -> { calendar, dotnetRef }

    function init(elementId, dotnetRef, initialEvents) {
        const el = document.getElementById(elementId);
        if (!el) return;

        // destroy if re-init
        if (calendars.has(elementId)) {
            calendars.get(elementId).calendar.destroy();
            calendars.delete(elementId);
        }

        const calendar = new FullCalendar.Calendar(el, {
            // Month only
            initialView: "dayGridMonth",
            height: "auto",

            // Only prev/next + title (no today, no week/day buttons)
            headerToolbar: {
                left: "prev,next",
                center: "title",
                right: ""
            },

            // keep navigation and click support
            navLinks: true,
            selectable: true,
            nowIndicator: true,

            // journal markers + events (if you still pass any)
            events: initialEvents || [],

            // click a day => go to journalentry with that date
            dateClick: (info) => {
                // info.dateStr is yyyy-MM-dd in dayGridMonth
                dotnetRef.invokeMethodAsync("OnDateClick", info.dateStr);
            },

            // click an event => OnEventClick(id)
            eventClick: (info) => {
                dotnetRef.invokeMethodAsync("OnEventClick", info.event.id);
            }
        });

        calendar.render();
        calendars.set(elementId, { calendar, dotnetRef });
    }

    function setEvents(elementId, events) {
        const entry = calendars.get(elementId);
        if (!entry) return;

        entry.calendar.removeAllEvents();
        (events || []).forEach((e) => entry.calendar.addEvent(e));
    }

    function addOrUpdateEvent(elementId, ev) {
        const entry = calendars.get(elementId);
        if (!entry) return;

        const existing = entry.calendar.getEventById(ev.id);
        if (existing) existing.remove();

        entry.calendar.addEvent(ev);
    }

    function removeEvent(elementId, eventId) {
        const entry = calendars.get(elementId);
        if (!entry) return;

        const existing = entry.calendar.getEventById(eventId);
        if (existing) existing.remove();
    }

    // optional helper (safe): force refresh size after navigation/layout changes
    function refresh(elementId) {
        const entry = calendars.get(elementId);
        if (!entry) return;
        entry.calendar.updateSize();
    }

    function destroy(elementId) {
        const entry = calendars.get(elementId);
        if (!entry) return;
        entry.calendar.destroy();
        calendars.delete(elementId);
    }

    return { init, setEvents, addOrUpdateEvent, removeEvent, refresh, destroy };
})();
