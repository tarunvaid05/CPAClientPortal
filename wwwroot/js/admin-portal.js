// Admin Portal JavaScript

document.addEventListener("DOMContentLoaded", () => {
    initializeAdminPortal()
})

let currentView = "categories" // Track current view: 'categories' or 'documents'
let currentClientId = ""
let cachedWorkflows = [] // Store workflows for viewWorkflowDetails access
let clientToDelete = null // Store client ID for delete confirmation

function initializeAdminPortal() {
    initializeClientSearch()
    initializeUploadFilters()
    initializeModals()
    initializeUploadStats()
    setDefaultDate()
    initializeAdminUploadForm()
    initializeSentToClientTab()
    initializeClientResponses()
}

// Client Search Functionality
function initializeClientSearch() {
    const searchInput = document.getElementById("clientSearch")
    if (searchInput) {
        searchInput.addEventListener("input", debounce(performClientSearch, 300))
    }
}

function performClientSearch() {
    const searchTerm = document.getElementById("clientSearch").value.toLowerCase()
    const clientCards = document.querySelectorAll(".client-card")

    clientCards.forEach((card) => {
        const clientName = card.getAttribute("data-client-name") || ""
        if (!searchTerm || clientName.includes(searchTerm)) {
            card.style.display = "flex"
        } else {
            card.style.display = "none"
        }
    })
}

// Upload Filters Functionality
function initializeUploadFilters() {
    const clientFilter = document.getElementById("clientFilter")
    const documentTypeFilter = document.getElementById("documentTypeFilter")
    const uploadSourceFilter = document.getElementById("uploadSourceFilter")
    const yearCheckboxes = document.querySelectorAll('input[type="checkbox"][id^="year"]')

    if (clientFilter) {
        clientFilter.addEventListener("change", performUploadFilter)
    }

    if (documentTypeFilter) {
        documentTypeFilter.addEventListener("change", performUploadFilter)
    }

    if (uploadSourceFilter) {
        uploadSourceFilter.addEventListener("change", performUploadFilter)
    }

    yearCheckboxes.forEach((checkbox) => {
        checkbox.addEventListener("change", performUploadFilter)
    })
}

function performUploadFilter() {
    const clientFilter = document.getElementById("clientFilter").value.toLowerCase()
    const documentTypeFilter = document.getElementById("documentTypeFilter").value.toLowerCase()
    const uploadSourceFilter = document.getElementById("uploadSourceFilter")?.value.toLowerCase() || ""
    const selectedYears = Array.from(document.querySelectorAll('input[type="checkbox"][id^="year"]:checked')).map(
        (cb) => cb.value,
    )

    const uploadItems = document.querySelectorAll(".upload-item")

    uploadItems.forEach((item) => {
        const client = item.getAttribute("data-client") || ""
        const type = item.getAttribute("data-type") || ""
        const year = item.getAttribute("data-year") || ""
        const source = item.getAttribute("data-source") || ""

        const matchesClient = !clientFilter || client.includes(clientFilter)
        const matchesType = !documentTypeFilter || type.includes(documentTypeFilter)
        const matchesYear = selectedYears.length === 0 || selectedYears.includes(year)
        const matchesSource = !uploadSourceFilter || source.includes(uploadSourceFilter)

        if (matchesClient && matchesType && matchesYear && matchesSource) {
            item.style.display = "flex"
        } else {
            item.style.display = "none"
        }
    })
}

// Upload Stats Functionality
function initializeUploadStats() {
    const periodFilter = document.getElementById("periodFilter")
    if (periodFilter) {
        periodFilter.addEventListener("change", updateUploadStats)
    }
}

function setDefaultDate() {
    const startDateInput = document.getElementById("startDate")
    if (startDateInput) {
        const today = new Date()
        const weekAgo = new Date(today.getTime() - 7 * 24 * 60 * 60 * 1000)
        startDateInput.value = weekAgo.toISOString().split("T")[0]
    }
}

function updateUploadStats() {
    const period = document.getElementById("periodFilter").value
    const startDate = document.getElementById("startDate").value

    // Simulate different counts based on period
    let count, label
    switch (period) {
        case "week":
            count = 15
            label = "Total Uploads This Week"
            break
        case "month":
            count = 67
            label = "Total Uploads This Month"
            break
        case "year":
            count = 342
            label = "Total Uploads This Year"
            break
        default:
            count = 15
            label = "Total Uploads This Week"
    }

    document.getElementById("uploadCount").textContent = count
    document.getElementById("uploadPeriod").textContent = label
}

// Client Modal Functionality
function openClientModal(clientId) {
    currentClientId = String(clientId)
    const clientCards = document.querySelectorAll(".client-card")
    let clientName = ""

    clientCards.forEach((card) => {
        if (String(card.getAttribute("data-client-id")) === currentClientId) {
            clientName = card.querySelector("h5").textContent
        }
    })

    document.getElementById("clientModalName").textContent = `${clientName} - Documents`

    // Reset to categories view
    currentView = "categories"
    updateSearchPlaceholder()

    // Reset modal tabs to first tab and clear admin upload form
    resetClientModalTabs()

    // Fetch and display client documents
    fetchClientDocuments(currentClientId)

    const modal = new bootstrap.Modal(document.getElementById("clientModal"))
    modal.show()
}

async function fetchClientDocuments(clientId) {
    try {
        const res = await fetch(`/Admin/DocumentCategories?userId=${encodeURIComponent(clientId)}`)
        const data = await res.json()
        const categories = (data && data.success ? data.categories : []) || []
        const mapped = categories.map(c => ({
            category: c.category || 'Other',
            count: c.count || 0,
            icon: pickIcon(c.category),
            lastUpdated: c.lastUpdated || ''
        }))
        displayDocumentCategories(mapped)
    } catch (e) {
        console.error(e)
        displayDocumentCategories([])
    }
}

function displayDocumentCategories(categories) {
    const grid = document.getElementById("documentCategoriesGrid")
    grid.innerHTML = ""

    categories.forEach((category) => {
        const categoryCard = document.createElement("div")
        categoryCard.className = "category-card"
        categoryCard.onclick = () => openCategoryDocuments(category.category)
        categoryCard.innerHTML = `
      <div class="category-icon">
        <i class="${category.icon}"></i>
      </div>
      <h6>${escapeHtml(category.category)}</h6>
      <p>${escapeHtml(category.count)} documents</p>
      <small class="text-muted">Updated ${escapeHtml(category.lastUpdated)}</small>
    `
        grid.appendChild(categoryCard)
    })

    // Show categories view
    document.getElementById("documentCategoriesView").style.display = "block"
    document.getElementById("categoryDocumentsView").style.display = "none"
}

async function openCategoryDocuments(categoryName) {
    document.getElementById("currentCategoryName").textContent = categoryName

    // Switch to documents view
    currentView = "documents"
    updateSearchPlaceholder()

    // Get selected years from modal
    const selectedYears = Array.from(document.querySelectorAll('input[type="checkbox"][id^="modalYear"]:checked')).map(
        (cb) => cb.value,
    )

    try {
        const params = new URLSearchParams()
        params.set('userId', currentClientId)
        params.set('category', categoryName)
        selectedYears.forEach(y => params.append('years', y))
        const res = await fetch(`/Admin/CategoryDocuments?${params.toString()}`)
        const data = await res.json()
        const docs = (data && data.success && Array.isArray(data.documents)) ? data.documents : []
        const documents = docs.map(d => ({
            id: d.id,
            fileName: d.fileName || d.name,
            uploadDate: (d.uploadedAt || d.uploadDate || '').toString().replace('T',' ').split('.')[0],
            fileSize: formatSize(d.fileSize || d.size),
            status: d.status || 'Uploaded'
        }))
        displayCategoryDocuments(documents)
    } catch (e) {
        console.error(e)
        displayCategoryDocuments([])
    }

    // Show documents view
    document.getElementById("documentCategoriesView").style.display = "none"
    document.getElementById("categoryDocumentsView").style.display = "block"

    // Initialize document search
    initializeUniversalSearch()
}

function displayCategoryDocuments(documents) {
    const documentsList = document.getElementById("documentsList")
    documentsList.innerHTML = ""

    documents.forEach((doc) => {
        const documentItem = document.createElement("div")
        documentItem.className = "document-item"
        documentItem.innerHTML = `
      <div class="document-item-info">
        <h6>${escapeHtml(doc.fileName)}</h6>
        <p>Uploaded: ${escapeHtml(doc.uploadDate)} - Size: ${escapeHtml(doc.fileSize || '')}</p>
      </div>
      <div class="document-item-actions">
        <div class="btn-group btn-group-sm" role="group">
          <a class="btn btn-outline-secondary" href="/Documents/Preview/${doc.id}" target="_blank"><i class="fas fa-eye me-1"></i>View</a>
          <a class="btn btn-outline-primary" href="/Documents/Download/${doc.id}"><i class="fas fa-download me-1"></i>Download</a>
          <button class="btn btn-outline-danger" onclick="adminDelete('${doc.id}')"><i class="fas fa-trash me-1"></i>Delete</button>
        </div>
      </div>
    `
        documentsList.appendChild(documentItem)
    })
}

async function adminDelete(id) {
    const tokenMeta = document.querySelector('meta[name="request-verification-token"]')
    const token = tokenMeta ? tokenMeta.getAttribute('content') : ''
    if (!confirm('Delete this document?')) return
    try {
        const res = await fetch(`/Documents/Delete/${id}`, { method: 'DELETE', headers: { 'RequestVerificationToken': token }})
        const data = await res.json()
        if (data && data.success) {
            showAlert('Document deleted', 'success')
            openCategoryDocuments(document.getElementById('currentCategoryName').textContent)
        } else {
            showAlert('Failed to delete', 'error')
        }
    } catch { showAlert('Failed to delete', 'error') }
}

// Delete Client Functionality
function confirmDeleteClient(clientId, clientName) {
    clientToDelete = clientId
    document.getElementById("deleteClientName").textContent = clientName
    const modal = new bootstrap.Modal(document.getElementById("deleteClientModal"))
    modal.show()
}

async function deleteClient() {
    if (!clientToDelete) return

    const confirmBtn = document.getElementById("confirmDeleteBtn")
    const originalText = confirmBtn.innerHTML
    confirmBtn.disabled = true
    confirmBtn.innerHTML = '<i class="fas fa-spinner fa-spin me-1"></i>Deleting...'

    try {
        const response = await fetch(`/Admin/DeleteClient?id=${clientToDelete}`, {
            method: "POST",
            headers: {
                "RequestVerificationToken": document.querySelector('input[name="__RequestVerificationToken"]').value
            }
        })

        const result = await response.json()

        // Close modal
        bootstrap.Modal.getInstance(document.getElementById("deleteClientModal")).hide()

        if (result.success) {
            // Remove client card from DOM
            const clientCard = document.querySelector(`.client-card[data-client-id="${clientToDelete}"]`)
            if (clientCard) {
                clientCard.remove()
            }
            showAlert(result.message, "success")
        } else {
            showAlert(result.message || "Failed to delete client.", "error")
        }
    } catch (error) {
        showAlert("An error occurred while deleting the client.", "error")
    } finally {
        confirmBtn.disabled = false
        confirmBtn.innerHTML = originalText
        clientToDelete = null
    }
}

// Deep-link to a specific document from All Uploads
async function openUploadInModal(ownerId, category, documentId) {
    console.log("openUploadInModal called:", { ownerId, category, documentId })

    if (!ownerId) {
        console.error("No ownerId provided")
        return
    }

    currentClientId = String(ownerId)

    // Find client name from client cards
    const clientCards = document.querySelectorAll(".client-card")
    let clientName = ""
    clientCards.forEach((card) => {
        if (String(card.getAttribute("data-client-id")) === currentClientId) {
            clientName = card.querySelector("h5").textContent
        }
    })

    const modalNameEl = document.getElementById("clientModalName")
    if (modalNameEl) {
        modalNameEl.textContent = clientName ? `${clientName} - Documents` : "Documents"
    }

    // Show the modal using getOrCreateInstance to avoid conflicts
    const modalElement = document.getElementById("clientModal")
    if (!modalElement) {
        console.error("Client modal not found")
        return
    }

    const modal = bootstrap.Modal.getOrCreateInstance(modalElement)
    modal.show()

    // Fetch categories first, then navigate to the specific category
    await fetchClientDocuments(currentClientId)

    // Small delay to ensure categories are rendered, then open the category
    setTimeout(async () => {
        await openCategoryDocuments(category)

        // After documents load, highlight the target document
        setTimeout(() => {
            highlightDocument(documentId)
        }, 300)
    }, 200)
}

function highlightDocument(documentId) {
    const documentsList = document.getElementById("documentsList")
    const items = documentsList.querySelectorAll(".document-item")

    items.forEach((item) => {
        // Find the download link which contains the document ID
        const downloadLink = item.querySelector('a[href*="/Documents/Download/"]')
        if (downloadLink && downloadLink.href.includes(documentId)) {
            item.classList.add("highlight-document")
            item.scrollIntoView({ behavior: 'smooth', block: 'center' })

            // Remove highlight after animation
            setTimeout(() => {
                item.classList.remove("highlight-document")
            }, 2000)
        }
    })
}

function pickIcon(category) {
    const map = {
        'w2': 'fas fa-file-invoice',
        '1099 int': 'fas fa-percentage',
        '1099': 'fas fa-percentage',
        '1098': 'fas fa-home',
        'schedule k-1': 'fas fa-users',
        'business income/expenses': 'fas fa-briefcase'
    }
    const key = (category || '').toLowerCase()
    return map[key] || 'fas fa-file'
}

function formatSize(bytes) {
    if (!bytes || isNaN(bytes)) return ''
    const kb = bytes / 1024
    if (kb < 1024) return `${kb.toFixed(1)} KB`
    const mb = kb / 1024
    return `${mb.toFixed(1)} MB`
}

function backToCategories() {
    currentView = "categories"
    updateSearchPlaceholder()

    document.getElementById("documentCategoriesView").style.display = "block"
    document.getElementById("categoryDocumentsView").style.display = "none"

    // Clear search
    document.getElementById("universalSearch").value = ""
}

// Universal Search Functionality
function updateSearchPlaceholder() {
    const searchInput = document.getElementById("universalSearch")
    if (searchInput) {
        if (currentView === "categories") {
            searchInput.placeholder = "Search categories..."
        } else {
            searchInput.placeholder = "Search documents..."
        }
    }
}

function initializeUniversalSearch() {
    const searchInput = document.getElementById("universalSearch")
    if (searchInput) {
        // Remove existing event listeners
        searchInput.removeEventListener("input", performCategorySearch)
        searchInput.removeEventListener("input", performDocumentSearch)

        // Add appropriate event listener based on current view
        if (currentView === "categories") {
            searchInput.addEventListener("input", debounce(performCategorySearch, 300))
        } else {
            searchInput.addEventListener("input", debounce(performDocumentSearch, 300))
        }
    }
}

function performCategorySearch() {
    const searchTerm = document.getElementById("universalSearch").value.toLowerCase()
    const categoryCards = document.querySelectorAll(".category-card")

    categoryCards.forEach((card) => {
        const categoryName = card.querySelector("h6").textContent.toLowerCase()
        if (!searchTerm || categoryName.includes(searchTerm)) {
            card.style.display = "block"
        } else {
            card.style.display = "none"
        }
    })
}

function performDocumentSearch() {
    const searchTerm = document.getElementById("universalSearch").value.toLowerCase()
    const documentItems = document.querySelectorAll(".document-item")

    documentItems.forEach((item) => {
        const fileName = item.querySelector("h6").textContent.toLowerCase()
        if (!searchTerm || fileName.includes(searchTerm)) {
            item.style.display = "flex"
        } else {
            item.style.display = "none"
        }
    })
}

// Reminder Modal Functionality
function openReminderModal() {
    // Reset views
    document.getElementById("clientSelectionView").style.display = "block"
    document.getElementById("emailTemplateView").style.display = "none"

    // Clear all checkboxes
    document.querySelectorAll('input[type="checkbox"][id^="remind_"]').forEach((cb) => {
        cb.checked = false
    })

    // Clear search
    const searchInput = document.getElementById("reminderClientSearch")
    if (searchInput) {
        searchInput.value = ""
        // Show all clients
        document.querySelectorAll(".reminder-client-item").forEach((item) => {
            item.style.display = ""
        })
        document.getElementById("reminderNoResults").style.display = "none"
    }

    // Initialize search functionality
    initializeReminderClientSearch()

    const modal = new bootstrap.Modal(document.getElementById("reminderModal"))
    modal.show()
}

function initializeReminderClientSearch() {
    const searchInput = document.getElementById("reminderClientSearch")
    if (!searchInput) return

    // Remove any existing listener
    searchInput.removeEventListener("input", performReminderClientSearch)
    searchInput.addEventListener("input", debounce(performReminderClientSearch, 200))
}

function performReminderClientSearch() {
    const searchTerm = document.getElementById("reminderClientSearch").value.toLowerCase().trim()
    const clientItems = document.querySelectorAll(".reminder-client-item")
    const noResultsDiv = document.getElementById("reminderNoResults")
    let visibleCount = 0

    clientItems.forEach((item) => {
        const name = item.getAttribute("data-name") || ""
        const email = item.getAttribute("data-email") || ""

        if (!searchTerm || name.includes(searchTerm) || email.includes(searchTerm)) {
            item.style.display = ""
            visibleCount++
        } else {
            item.style.display = "none"
        }
    })

    if (noResultsDiv) {
        noResultsDiv.style.display = visibleCount === 0 && searchTerm ? "block" : "none"
    }
}

function showEmailTemplate() {
    const selectedClients = Array.from(document.querySelectorAll('input[type="checkbox"][id^="remind_"]:checked'))

    if (selectedClients.length === 0) {
        showAlert("Please select at least one client to remind.", "warning")
        return
    }

    document.getElementById("clientSelectionView").style.display = "none"
    document.getElementById("emailTemplateView").style.display = "block"
}

function backToClientSelection() {
    document.getElementById("clientSelectionView").style.display = "block"
    document.getElementById("emailTemplateView").style.display = "none"
}

async function sendReminders() {
    const selectedClientIds = Array.from(document.querySelectorAll('input[type="checkbox"][id^="remind_"]:checked')).map(
        (cb) => String(cb.value),
    )

    const emailBody = document.getElementById("emailBody").value
    const emailSubject = document.getElementById("emailSubject").value

    if (selectedClientIds.length === 0) {
        showAlert("No clients selected.", "warning")
        return
    }

    if (!emailBody.trim()) {
        showAlert("Please enter an email message.", "warning")
        return
    }

    if (!emailSubject.trim()) {
        showAlert("Please enter an email subject.", "warning")
        return
    }

    // Get the send button and show loading state
    const sendBtn = document.querySelector('#emailTemplateView .btn-success')
    const originalBtnHtml = sendBtn ? sendBtn.innerHTML : ''
    if (sendBtn) {
        sendBtn.disabled = true
        sendBtn.innerHTML = '<span class="spinner-border spinner-border-sm me-2"></span>Sending...'
    }

    try {
        const tokenMeta = document.querySelector('meta[name="request-verification-token"]')
        const token = tokenMeta ? tokenMeta.getAttribute('content') : ''

        const response = await fetch('/Admin/SendReminder', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': token
            },
            body: JSON.stringify({
                clientIds: selectedClientIds,
                subject: emailSubject,
                body: emailBody
            })
        })

        const data = await response.json()

        if (response.ok && data.success) {
            showAlert(data.message || `Reminders sent successfully to ${selectedClientIds.length} clients!`, "success")
            const modal = bootstrap.Modal.getInstance(document.getElementById("reminderModal"))
            if (modal) modal.hide()
        } else {
            showAlert(data.message || "Failed to send reminders. Please try again.", "error")
        }
    } catch (error) {
        console.error("Error sending reminders:", error)
        showAlert("Failed to send reminders. Please check your connection and try again.", "error")
    } finally {
        if (sendBtn) {
            sendBtn.disabled = false
            sendBtn.innerHTML = originalBtnHtml
        }
    }
}

// Email template editing is integrated in the reminder flow now; standalone template modal removed.

// Modal Initialization
function initializeModals() {
    // Profile form
    const profileForm = document.getElementById("profileForm")
    if (profileForm) {
        profileForm.addEventListener("submit", handleProfileUpdate)
    }

    // Password form
    const passwordForm = document.getElementById("passwordForm")
    if (passwordForm) {
        passwordForm.addEventListener("submit", handlePasswordUpdate)
    }

    // Notification form
    const notificationForm = document.getElementById("notificationForm")
    if (notificationForm) {
        notificationForm.addEventListener("submit", handleNotificationUpdate)
    }

    // Invite client form
    const inviteForm = document.getElementById("inviteForm")
    if (inviteForm) {
        inviteForm.addEventListener("submit", handleInviteSubmit)
    }
    // Defensive: delegate submit to catch any late-bound form
    document.addEventListener("submit", (e) => {
        const target = e.target
        if (target && target.id === "inviteForm") {
            e.preventDefault()
            handleInviteSubmit(e)
        }
    })

    // Initialize universal search when modal opens
    const clientModalEl = document.getElementById("clientModal")
    if (clientModalEl) {
        clientModalEl.addEventListener("shown.bs.modal", () => {
            initializeUniversalSearch()
        })
    }
}

// Profile Management Functions
function handleProfileUpdate(event) {
    event.preventDefault()

    const email = document.getElementById("profileEmail").value
    const phone = document.getElementById("profilePhone").value

    // Simulate API call
    setTimeout(() => {
        showAlert("Profile updated successfully!", "success")
        const modal = bootstrap.Modal.getInstance(document.getElementById("profileModal"))
        modal.hide()
    }, 1000)
}

function handlePasswordUpdate(event) {
    event.preventDefault()

    const currentPassword = document.getElementById("currentPassword").value
    const newPassword = document.getElementById("newPassword").value
    const confirmPassword = document.getElementById("confirmPassword").value

    if (newPassword !== confirmPassword) {
        showAlert("New passwords do not match.", "error")
        return
    }

    // Simulate API call
    setTimeout(() => {
        showAlert("Password updated successfully!", "success")
        const modal = bootstrap.Modal.getInstance(document.getElementById("passwordModal"))
        modal.hide()

        // Clear form
        document.getElementById("passwordForm").reset()
    }, 1000)
}

function handleNotificationUpdate(event) {
    event.preventDefault()

    // Simulate API call
    setTimeout(() => {
        showAlert("Notification settings updated!", "success")
        const modal = bootstrap.Modal.getInstance(document.getElementById("notificationModal"))
        modal.hide()
    }, 1000)
}

// Invite Client
function openInviteModal() {
    const modal = new bootstrap.Modal(document.getElementById("inviteClientModal"))
    modal.show()
}

async function handleInviteSubmit(event) {
    event.preventDefault()
    const form = event.target
    const formData = new FormData(form)
    const emailInput = document.getElementById("inviteEmail")
    const emailError = document.getElementById("inviteEmailError")
    const generalError = document.getElementById("inviteGeneralError")
    const submitBtn = document.getElementById("inviteSubmitBtn")

    // Reset errors
    if (emailInput) emailInput.classList.remove("is-invalid")
    if (emailError) {
        emailError.style.display = "none"
        emailError.textContent = ""
    }
    if (generalError) {
        generalError.classList.add("d-none")
        generalError.classList.remove("alert-warning", "alert-danger")
        generalError.classList.add("alert-danger")
        generalError.textContent = ""
    }

    // Basic client-side required check
    if (!form.checkValidity()) {
        showAlert("Please fill all required fields.", "warning")
        return
    }

    if (submitBtn) submitBtn.disabled = true

    try {
        const tokenMeta = document.querySelector('meta[name="request-verification-token"]')
        const token = tokenMeta ? tokenMeta.getAttribute('content') : ''
        const response = await fetch(form.getAttribute("action"), {
            method: "POST",
            body: formData,
            headers: { "X-Requested-With": "XMLHttpRequest", "RequestVerificationToken": token },
        })

        let result
        const ct = response.headers.get("content-type") || ""
        if (response.ok && ct.includes("application/json")) {
            result = await response.json()
        } else {
            // Fallback: treat non-JSON as error
            result = { success: false, message: response.status >= 400 ? `Request failed (${response.status})` : "Unexpected response from server." }
        }

        if (result && result.success) {
            // Check if this is a re-send to a pending user who never completed setup
            if (result.isResend) {
                showAlert("This client was previously invited but hasn't completed account setup. A new invitation email has been sent and the previous link is no longer valid.", "warning")
            }

            const isEmailSent = typeof result.emailSent === "undefined" ? true : !!result.emailSent
            const type = isEmailSent ? "success" : "warning"
            showAlert(result.message || (isEmailSent ? "Invitation sent successfully!" : "User created, but email failed."), type)
            const modal = bootstrap.Modal.getInstance(document.getElementById("inviteClientModal"))
            if (modal) modal.hide()
            form.reset()
        } else {
            const msg = result.message || (result.errors && result.errors.join(", ")) || "Failed to send invite."
            // Inline errors
            if (msg.toLowerCase().includes("already exists") && emailInput && emailError) {
                emailInput.classList.add("is-invalid")
                emailError.textContent = msg
                emailError.style.display = "block"
            } else if (generalError) {
                generalError.textContent = msg
                generalError.classList.remove("d-none")
                // If it's about email sending, show as warning
                if (msg.toLowerCase().includes("failed to send") || msg.toLowerCase().includes("email")) {
                    generalError.classList.remove("alert-danger")
                    generalError.classList.add("alert-warning")
                }
            } else {
                showAlert(msg, "warning")
            }
        }
    } catch (err) {
        console.error(err)
        if (generalError) {
            generalError.textContent = "Failed to send invite."
            generalError.classList.remove("d-none")
        } else {
            showAlert("Failed to send invite.", "error")
        }
    }
    finally {
        if (submitBtn) submitBtn.disabled = false
    }
}

// Utility Functions
function showAlert(message, type) {
    const alertElement = document.createElement("div")
    alertElement.className = `alert alert-${type === "error" ? "danger" : type} alert-dismissible alert-notification`
    alertElement.innerHTML = `
        <i class="fas fa-${getAlertIcon(type)} me-2"></i>
        ${escapeHtml(message)}
        <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>
    `

    document.body.appendChild(alertElement)

    // Auto-dismiss after 5 seconds
    setTimeout(() => {
        if (alertElement.parentNode) {
            alertElement.remove()
        }
    }, 5000)
}

function getAlertIcon(type) {
    switch (type) {
        case "success":
            return "check-circle"
        case "warning":
            return "exclamation-triangle"
        case "error":
            return "exclamation-circle"
        case "info":
            return "info-circle"
        default:
            return "info-circle"
    }
}

function debounce(func, wait) {
    let timeout
    return function executedFunction(...args) {
        const later = () => {
            clearTimeout(timeout)
            func(...args)
        }
        clearTimeout(timeout)
        timeout = setTimeout(later, wait)
    }
}

// Admin Document Sending Functionality

// Toggle admin upload form visibility
function toggleAdminUploadForm() {
    const form = document.getElementById("adminUploadForm")
    const button = document.getElementById("toggleSendDocumentForm")

    if (form.style.display === "none") {
        form.style.display = "block"
        button.innerHTML = '<i class="fas fa-times me-2"></i>Cancel'
        button.classList.remove("btn-primary")
        button.classList.add("btn-outline-secondary")
        // Set the client ID for the form
        document.getElementById("adminUploadClientId").value = currentClientId
    } else {
        form.style.display = "none"
        button.innerHTML = '<i class="fas fa-plus me-2"></i>Send New Document'
        button.classList.remove("btn-outline-secondary")
        button.classList.add("btn-primary")
        // Reset the form
        document.getElementById("adminDocumentUploadForm").reset()
    }
}

// Initialize admin upload form submission handler
function initializeAdminUploadForm() {
    const form = document.getElementById("adminDocumentUploadForm")
    if (form) {
        form.addEventListener("submit", handleAdminUploadSubmit)
    }
}

// Handle admin document upload form submission
async function handleAdminUploadSubmit(event) {
    event.preventDefault()

    const form = event.target
    const submitBtn = form.querySelector('button[type="submit"]')
    const originalBtnHtml = submitBtn.innerHTML

    // Get form data
    const formData = new FormData(form)

    // Validate
    const file = document.getElementById("adminUploadFile").files[0]
    const category = document.getElementById("adminUploadCategory").value
    const clientId = document.getElementById("adminUploadClientId").value

    if (!file) {
        showAlert("Please select a file to upload.", "warning")
        return
    }

    if (!category) {
        showAlert("Please select a category.", "warning")
        return
    }

    if (!clientId) {
        showAlert("Client ID is missing.", "error")
        return
    }

    // Show loading state
    submitBtn.disabled = true
    submitBtn.innerHTML = '<span class="spinner-border spinner-border-sm me-2"></span>Uploading...'

    try {
        const tokenMeta = document.querySelector('meta[name="request-verification-token"]')
        const token = tokenMeta ? tokenMeta.getAttribute('content') : ''

        const response = await fetch('/Documents/AdminUpload', {
            method: 'POST',
            headers: {
                'RequestVerificationToken': token
            },
            body: formData
        })

        const data = await response.json()

        if (response.ok && data.success) {
            showAlert(data.message || "Document sent to client successfully!", "success")

            // Hide form and reset
            toggleAdminUploadForm()

            // Refresh the sent documents list
            await fetchSentDocuments(currentClientId)
        } else {
            showAlert(data.message || "Failed to send document. Please try again.", "error")
        }
    } catch (error) {
        console.error("Error uploading document:", error)
        showAlert("Failed to send document. Please check your connection and try again.", "error")
    } finally {
        submitBtn.disabled = false
        submitBtn.innerHTML = originalBtnHtml
    }
}

// Fetch documents sent by admin to the client
async function fetchSentDocuments(clientId) {
    const sentDocumentsList = document.getElementById("sentDocumentsList")
    const emptyMessage = document.getElementById("sentDocumentsEmpty")

    if (!sentDocumentsList) return

    // Show loading state
    sentDocumentsList.innerHTML = '<div class="text-center py-4"><span class="spinner-border spinner-border-sm me-2"></span>Loading...</div>'

    try {
        const response = await fetch(`/Admin/SentDocuments?userId=${encodeURIComponent(clientId)}`)
        const data = await response.json()

        if (response.ok && data.success && Array.isArray(data.documents) && data.documents.length > 0) {
            displaySentDocuments(data.documents)
        } else {
            // Show empty state
            sentDocumentsList.innerHTML = `
                <div class="text-center text-muted py-4" id="sentDocumentsEmpty">
                    <i class="fas fa-inbox fa-2x mb-2"></i>
                    <p>No documents sent to this client yet.</p>
                </div>
            `
        }
    } catch (error) {
        console.error("Error fetching sent documents:", error)
        sentDocumentsList.innerHTML = `
            <div class="text-center text-danger py-4">
                <i class="fas fa-exclamation-triangle fa-2x mb-2"></i>
                <p>Failed to load sent documents.</p>
            </div>
        `
    }
}

// Display sent documents in the list
function displaySentDocuments(documents) {
    const sentDocumentsList = document.getElementById("sentDocumentsList")
    sentDocumentsList.innerHTML = ""

    documents.forEach((doc) => {
        const documentItem = document.createElement("div")
        documentItem.className = "document-item"

        // Check if it requires signature for special badge
        const isRequiresSignature = doc.category && doc.category.toLowerCase().includes("requires signature")
        const categoryBadgeClass = isRequiresSignature ? "bg-warning text-dark" : "bg-info"
        const signatureIcon = isRequiresSignature ? '<i class="fas fa-exclamation-triangle me-1"></i>' : ''

        const uploadDate = (doc.uploadedAt || doc.uploadDate || '').toString().replace('T', ' ').split('.')[0]
        const fileSize = formatSize(doc.fileSize || doc.size)

        documentItem.innerHTML = `
            <div class="document-item-info">
                <h6>${escapeHtml(doc.fileName || doc.originalFileName)}</h6>
                <p>
                    <span class="badge ${categoryBadgeClass} me-2">${signatureIcon}${escapeHtml(doc.category || 'Unknown')}</span>
                    Sent: ${escapeHtml(uploadDate)}${fileSize ? ' - Size: ' + escapeHtml(fileSize) : ''}
                </p>
            </div>
            <div class="document-item-actions">
                <a class="btn btn-sm btn-outline-primary" href="/Documents/Download/${doc.id}">
                    <i class="fas fa-download me-1"></i>Download
                </a>
            </div>
        `
        sentDocumentsList.appendChild(documentItem)
    })
}

// Initialize tab event listener for "Sent to Client" tab
function initializeSentToClientTab() {
    const sentToClientTab = document.getElementById("sentToClient-tab")
    if (sentToClientTab) {
        sentToClientTab.addEventListener("shown.bs.tab", () => {
            if (currentClientId) {
                fetchSentDocuments(currentClientId)
            }
        })
    }
}

// Reset modal tabs when client modal opens
function resetClientModalTabs() {
    // Reset to first tab
    const clientUploadsTab = document.getElementById("clientUploads-tab")
    if (clientUploadsTab) {
        const tab = new bootstrap.Tab(clientUploadsTab)
        tab.show()
    }

    // Hide admin upload form if visible
    const adminUploadForm = document.getElementById("adminUploadForm")
    if (adminUploadForm) {
        adminUploadForm.style.display = "none"
    }

    // Reset the toggle button
    const toggleBtn = document.getElementById("toggleSendDocumentForm")
    if (toggleBtn) {
        toggleBtn.innerHTML = '<i class="fas fa-plus me-2"></i>Send New Document'
        toggleBtn.classList.remove("btn-outline-secondary")
        toggleBtn.classList.add("btn-primary")
    }

    // Reset the form
    const form = document.getElementById("adminDocumentUploadForm")
    if (form) {
        form.reset()
    }
}

// Client Responses Functionality

// Initialize client responses section
function initializeClientResponses() {
    const statusFilter = document.getElementById("responseStatusFilter")
    if (statusFilter) {
        statusFilter.addEventListener("change", () => {
            fetchClientResponses(statusFilter.value)
        })
        // Initial load with default filter (responded)
        fetchClientResponses(statusFilter.value)
    }
}

// Fetch client responses/workflows from the server
async function fetchClientResponses(status) {
    const responsesList = document.getElementById("clientResponsesList")
    if (!responsesList) return

    // Show loading state
    responsesList.innerHTML = `
        <div class="text-center text-muted py-3">
            <span class="spinner-border spinner-border-sm"></span> Loading...
        </div>
    `

    try {
        const params = new URLSearchParams()
        if (status) {
            params.set("status", status)
        }

        const response = await fetch(`/Admin/GetWorkflows?${params.toString()}`)
        const data = await response.json()

        if (response.ok && data.success && Array.isArray(data.workflows)) {
            displayClientResponses(data.workflows)
        } else {
            responsesList.innerHTML = `
                <div class="text-center text-muted py-3">
                    <i class="fas fa-inbox"></i>
                    <p class="mb-0">No responses found.</p>
                </div>
            `
        }
    } catch (error) {
        console.error("Error fetching client responses:", error)
        responsesList.innerHTML = `
            <div class="text-center text-danger py-3">
                <i class="fas fa-exclamation-triangle"></i>
                <p class="mb-0">Failed to load responses.</p>
            </div>
        `
    }
}

// Display client responses in the list
function displayClientResponses(workflows) {
    // Cache workflows globally for viewWorkflowDetails access
    cachedWorkflows = workflows

    const responsesList = document.getElementById("clientResponsesList")
    responsesList.innerHTML = ""

    if (workflows.length === 0) {
        responsesList.innerHTML = `
            <div class="text-center text-muted py-3">
                <i class="fas fa-inbox"></i>
                <p class="mb-0">No responses found.</p>
            </div>
        `
        return
    }

    workflows.forEach((workflow) => {
        const responseItem = document.createElement("div")
        responseItem.className = "response-item"
        responseItem.setAttribute("data-workflow-id", workflow.id)

        // Format time ago
        const timeAgo = formatTimeAgo(workflow.respondedAt || workflow.createdAt)

        // Status badge
        let statusBadge = ""
        switch (workflow.status.toLowerCase()) {
            case "pending":
                statusBadge = '<span class="badge bg-warning text-dark">Pending</span>'
                break
            case "responded":
                statusBadge = '<span class="badge bg-info">Responded</span>'
                break
            case "resolved":
                statusBadge = '<span class="badge bg-success">Resolved</span>'
                break
            default:
                statusBadge = `<span class="badge bg-secondary">${escapeHtml(workflow.status)}</span>`
        }

        // Response preview (truncated)
        let responsePreview = ""
        if (workflow.clientResponseText) {
            const truncatedText = workflow.clientResponseText.length > 80
                ? workflow.clientResponseText.substring(0, 80) + "..."
                : workflow.clientResponseText
            responsePreview = `<p class="response-preview text-muted small mb-0">"${escapeHtml(truncatedText)}"</p>`
        }

        // Resolve button (only show for Responded status)
        let resolveButton = ""
        if (workflow.status.toLowerCase() === "responded") {
            resolveButton = `<button class="btn btn-sm btn-success" onclick="resolveWorkflow('${workflow.id}')"><i class="fas fa-check me-1"></i>Resolve</button>`
        }

        responseItem.innerHTML = `
            <div class="response-info">
                <h6 class="mb-1">${escapeHtml(workflow.clientName)} - ${escapeHtml(workflow.documentName)}</h6>
                <p class="text-muted small mb-1">${escapeHtml(timeAgo)} ${statusBadge}</p>
                ${responsePreview}
            </div>
            <div class="response-actions">
                <button class="btn btn-sm btn-outline-primary me-1" onclick="viewWorkflowDetails('${workflow.id}')">View</button>
                ${resolveButton}
            </div>
        `

        responsesList.appendChild(responseItem)
    })
}

// Format time ago string
function formatTimeAgo(dateString) {
    if (!dateString) return ""

    const date = new Date(dateString)
    const now = new Date()
    const diffMs = now - date
    const diffMins = Math.floor(diffMs / 60000)
    const diffHours = Math.floor(diffMs / 3600000)
    const diffDays = Math.floor(diffMs / 86400000)

    if (diffMins < 1) return "Just now"
    if (diffMins < 60) return `${diffMins}m ago`
    if (diffHours < 24) return `${diffHours}h ago`
    if (diffDays < 7) return `${diffDays}d ago`

    return date.toLocaleDateString()
}

// View workflow details in modal
function viewWorkflowDetails(workflowId) {
    // Find the workflow in cached data
    const workflow = cachedWorkflows.find(w => w.id === workflowId)
    if (!workflow) {
        showAlert("Workflow not found.", "error")
        return
    }

    // Populate status badge
    const statusBadge = document.getElementById("workflowStatusBadge")
    const status = (workflow.status || "").toLowerCase()
    let badgeClass = "bg-secondary"
    let badgeText = workflow.status
    switch (status) {
        case "pending":
            badgeClass = "bg-warning text-dark"
            badgeText = "Pending"
            break
        case "responded":
            badgeClass = "bg-info"
            badgeText = "Responded"
            break
        case "resolved":
            badgeClass = "bg-success"
            badgeText = "Resolved"
            break
    }
    statusBadge.className = `badge ms-2 ${badgeClass}`
    statusBadge.textContent = badgeText

    // Build timeline
    const timeline = document.getElementById("workflowTimeline")
    let timelineHtml = ""

    // Sent date (createdAt)
    if (workflow.createdAt) {
        const sentDate = new Date(workflow.createdAt).toLocaleString()
        timelineHtml += `
            <div class="d-flex align-items-center mb-2">
                <span class="badge bg-primary me-2"><i class="fas fa-paper-plane"></i></span>
                <span><strong>Sent:</strong> ${sentDate}</span>
            </div>
        `
    }

    // Responded date
    if (workflow.respondedAt) {
        const respondedDate = new Date(workflow.respondedAt).toLocaleString()
        timelineHtml += `
            <div class="d-flex align-items-center mb-2">
                <span class="badge bg-info me-2"><i class="fas fa-reply"></i></span>
                <span><strong>Responded:</strong> ${respondedDate}</span>
            </div>
        `
    }

    // Resolved date
    if (workflow.resolvedAt) {
        const resolvedDate = new Date(workflow.resolvedAt).toLocaleString()
        timelineHtml += `
            <div class="d-flex align-items-center mb-2">
                <span class="badge bg-success me-2"><i class="fas fa-check"></i></span>
                <span><strong>Resolved:</strong> ${resolvedDate}</span>
            </div>
        `
    }

    timeline.innerHTML = timelineHtml || '<p class="text-muted">No timeline data available.</p>'

    // Populate document info
    document.getElementById("workflowDocumentName").textContent = workflow.documentName || "Unknown"
    document.getElementById("workflowCategory").textContent = workflow.category || "Unknown"
    document.getElementById("workflowClientName").textContent = workflow.clientName || "Unknown"

    // Admin notes
    const adminNotesSection = document.getElementById("adminNotesSection")
    const adminNotesText = document.getElementById("workflowAdminNotes")
    if (workflow.adminNotes) {
        adminNotesSection.style.display = "block"
        adminNotesText.textContent = workflow.adminNotes
    } else {
        adminNotesSection.style.display = "none"
    }

    // Download original document button
    const downloadOriginalBtn = document.getElementById("downloadOriginalBtn")
    if (workflow.documentId) {
        downloadOriginalBtn.onclick = () => {
            window.location.href = `/Documents/Download/${workflow.documentId}`
        }
        downloadOriginalBtn.style.display = "inline-block"
    } else {
        downloadOriginalBtn.style.display = "none"
    }

    // Client response section
    const clientResponseSection = document.getElementById("clientResponseSection")
    const responseTextSection = document.getElementById("responseTextSection")
    const workflowResponseText = document.getElementById("workflowResponseText")
    const downloadResponseBtn = document.getElementById("downloadResponseBtn")

    if (status === "responded" || status === "resolved") {
        clientResponseSection.style.display = "block"

        // Response text
        if (workflow.clientResponseText) {
            responseTextSection.style.display = "block"
            workflowResponseText.textContent = workflow.clientResponseText
        } else {
            responseTextSection.style.display = "none"
        }

        // Response document download
        if (workflow.responseDocumentId) {
            downloadResponseBtn.onclick = () => {
                window.location.href = `/Documents/Download/${workflow.responseDocumentId}`
            }
            downloadResponseBtn.style.display = "inline-block"
        } else {
            downloadResponseBtn.style.display = "none"
        }
    } else {
        clientResponseSection.style.display = "none"
    }

    // Resolve button (only show for "responded" status)
    const resolveWorkflowBtn = document.getElementById("resolveWorkflowBtn")
    if (status === "responded") {
        resolveWorkflowBtn.style.display = "inline-block"
        resolveWorkflowBtn.onclick = () => {
            resolveWorkflow(workflowId)
            // Close modal after resolving
            const modal = bootstrap.Modal.getInstance(document.getElementById("workflowDetailsModal"))
            if (modal) modal.hide()
        }
    } else {
        resolveWorkflowBtn.style.display = "none"
    }

    // Show the modal
    const modal = new bootstrap.Modal(document.getElementById("workflowDetailsModal"))
    modal.show()
}

// Resolve a workflow
async function resolveWorkflow(workflowId) {
    if (!confirm("Mark this workflow as resolved?")) return

    try {
        const tokenMeta = document.querySelector('meta[name="request-verification-token"]')
        const token = tokenMeta ? tokenMeta.getAttribute("content") : ""

        const response = await fetch("/Admin/ResolveWorkflow", {
            method: "POST",
            headers: {
                "Content-Type": "application/x-www-form-urlencoded",
                "RequestVerificationToken": token
            },
            body: `id=${encodeURIComponent(workflowId)}`
        })

        const data = await response.json()

        if (response.ok && data.success) {
            showAlert("Workflow marked as resolved.", "success")
            // Refresh the list
            const statusFilter = document.getElementById("responseStatusFilter")
            fetchClientResponses(statusFilter ? statusFilter.value : "")
        } else {
            showAlert("Failed to resolve workflow.", "error")
        }
    } catch (error) {
        console.error("Error resolving workflow:", error)
        showAlert("Failed to resolve workflow.", "error")
    }
}

// Ensure Bootstrap is available
const bootstrap = window.bootstrap
if (typeof bootstrap === "undefined") {
    console.error("Bootstrap is not loaded")
} else {
    console.log("Bootstrap loaded successfully")
}

// Escape HTML before interpolating into innerHTML. Client-supplied values reach this
// dashboard (file names, response text, client names), so they must never be treated
// as markup - otherwise a client can run script in the admin's authenticated session.
function escapeHtml(text) {
    if (text === null || text === undefined) return ''
    const div = document.createElement('div')
    div.textContent = text
    return div.innerHTML
}

