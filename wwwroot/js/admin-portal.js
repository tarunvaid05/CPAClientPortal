// Admin Portal JavaScript

document.addEventListener("DOMContentLoaded", () => {
    initializeAdminPortal()
})

let currentView = "categories" // Track current view: 'categories' or 'documents'
let currentClientId = ""

function initializeAdminPortal() {
    initializeClientSearch()
    initializeUploadFilters()
    initializeModals()
    initializeUploadStats()
    setDefaultDate()
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
    const yearCheckboxes = document.querySelectorAll('input[type="checkbox"][id^="year"]')

    if (clientFilter) {
        clientFilter.addEventListener("change", performUploadFilter)
    }

    if (documentTypeFilter) {
        documentTypeFilter.addEventListener("change", performUploadFilter)
    }

    yearCheckboxes.forEach((checkbox) => {
        checkbox.addEventListener("change", performUploadFilter)
    })
}

function performUploadFilter() {
    const clientFilter = document.getElementById("clientFilter").value.toLowerCase()
    const documentTypeFilter = document.getElementById("documentTypeFilter").value.toLowerCase()
    const selectedYears = Array.from(document.querySelectorAll('input[type="checkbox"][id^="year"]:checked')).map(
        (cb) => cb.value,
    )

    const uploadItems = document.querySelectorAll(".upload-item")

    uploadItems.forEach((item) => {
        const client = item.getAttribute("data-client") || ""
        const type = item.getAttribute("data-type") || ""
        const year = item.getAttribute("data-year") || ""

        const matchesClient = !clientFilter || client.includes(clientFilter)
        const matchesType = !documentTypeFilter || type.includes(documentTypeFilter)
        const matchesYear = selectedYears.length === 0 || selectedYears.includes(year)

        if (matchesClient && matchesType && matchesYear) {
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
      <h6>${category.category}</h6>
      <p>${category.count} documents</p>
      <small class="text-muted">Updated ${category.lastUpdated}</small>
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
        <h6>${doc.fileName}</h6>
        <p>Uploaded: ${doc.uploadDate} • Size: ${doc.fileSize || ''}</p>
      </div>
      <div class="document-item-actions">
        <div class="btn-group btn-group-sm" role="group">
          <a class="btn btn-outline-primary" href="/Documents/Download/${doc.id}"><i class="fas fa-download me-1"></i>Download</a>
          <button class="btn btn-outline-danger" onclick="adminDelete('${'${doc.id}'}')"><i class="fas fa-trash me-1"></i>Delete</button>
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

    const modal = new bootstrap.Modal(document.getElementById("reminderModal"))
    modal.show()
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

function sendReminders() {
    const selectedClientIds = Array.from(document.querySelectorAll('input[type="checkbox"][id^="remind_"]:checked')).map(
        (cb) => String(cb.value),
    )

    const emailContent = document.getElementById("emailBody").value
    const emailSubject = document.getElementById("emailSubject").value

    if (selectedClientIds.length === 0) {
        showAlert("No clients selected.", "warning")
        return
    }

    if (!emailContent.trim()) {
        showAlert("Please enter an email message.", "warning")
        return
    }

    // Simulate sending reminders
    setTimeout(() => {
        showAlert(`Reminders sent successfully to ${selectedClientIds.length} clients!`, "success")
        const modal = bootstrap.Modal.getInstance(document.getElementById("reminderModal"))
        modal.hide()
    }, 1000)
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
    const alertContainer = document.querySelector(".container-fluid")
    const alertElement = document.createElement("div")
    alertElement.className = `alert alert-${type === "error" ? "danger" : type} alert-dismissible fade show`
    alertElement.innerHTML = `
    <i class="fas fa-${getAlertIcon(type)} me-2"></i>
    ${message}
    <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>
  `

    alertContainer.insertBefore(alertElement, alertContainer.firstChild)

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

// Ensure Bootstrap is available
const bootstrap = window.bootstrap
if (typeof bootstrap === "undefined") {
    console.error("Bootstrap is not loaded")
} else {
    console.log("Bootstrap loaded successfully")
}

