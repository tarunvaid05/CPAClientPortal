// Client Portal JavaScript

// Keep in sync with Services/UploadPolicy.cs
const MAX_UPLOAD_MB = 25
const MAX_UPLOAD_BYTES = MAX_UPLOAD_MB * 1024 * 1024

document.addEventListener("DOMContentLoaded", () => {
    initializeClientPortal()
})

function initializeClientPortal() {
    initializeFileUploads()
    initializeUploadsSearch()
    initializeDocumentCards()
    initDocumentTypeSearch()
    initializeModals()
    initializeDragAndDrop()
    initializeCpaDocsFilter()
    initializeContactForm()
    initializeAppointmentForm()

    // Load received documents with smart default filter
    initializeCpaDocsWithSmartDefault()
}

// Initialize CPA Documents section with smart default filter
// Priority: Pending (if any exist) -> Responded (if any exist) -> Pending (fallback)
async function initializeCpaDocsWithSmartDefault() {
    const filterDropdown = document.getElementById("cpaDocsStatusFilter")

    try {
        // Fetch pending and responded counts in parallel
        const [pendingResponse, respondedResponse] = await Promise.all([
            fetch("/ClientPortal/GetWorkflows?status=pending"),
            fetch("/ClientPortal/GetWorkflows?status=responded")
        ])

        const pendingData = await pendingResponse.json()
        const respondedData = await respondedResponse.json()

        const pendingCount = pendingData.success ? pendingData.workflows.length : 0
        const respondedCount = respondedData.success ? respondedData.workflows.length : 0

        // Determine smart default: pending if any exist, else responded if any exist, else pending
        let defaultStatus = "pending"
        if (pendingCount === 0 && respondedCount > 0) {
            defaultStatus = "responded"
        }

        // Update dropdown to match smart default
        if (filterDropdown) {
            filterDropdown.value = defaultStatus
        }

        // Load documents with the smart default
        loadReceivedDocuments(defaultStatus)
    } catch (error) {
        console.error("Error determining smart default filter:", error)
        // Fallback to pending on error
        loadReceivedDocuments("pending")
    }
}

// Initialize CPA Documents filter
function initializeCpaDocsFilter() {
    const cpaDocsFilter = document.getElementById("cpaDocsStatusFilter")
    if (cpaDocsFilter) {
        cpaDocsFilter.addEventListener("change", () => {
            loadReceivedDocuments(cpaDocsFilter.value)
        })
    }
}

// File Upload Functionality
function initializeFileUploads() {
    // Modal file upload
    const modalUploadArea = document.getElementById("modalUploadArea")
    const modalFileInput = document.getElementById("documentFileInput")

    if (modalUploadArea && modalFileInput) {
        modalUploadArea.addEventListener("click", () => modalFileInput.click())
        modalFileInput.addEventListener("change", handleModalFileSelect)
    }

    // Upload button handlers
    const uploadDocumentBtn = document.getElementById("uploadDocumentBtn")
    if (uploadDocumentBtn) {
        uploadDocumentBtn.addEventListener("click", uploadSelectedFiles)
    }
}

// Uploads Search Functionality
function initializeUploadsSearch() {
    const searchInput = document.getElementById("uploadsSearch")
    const searchBtn = document.getElementById("uploadsSearchBtn")
    const filterSelect = document.getElementById("uploadsFilter")

    if (searchInput) {
        searchInput.addEventListener("input", debounce(performUploadsSearch, 300))
    }

    if (searchBtn) {
        searchBtn.addEventListener("click", performUploadsSearch)
    }

    if (filterSelect) {
        filterSelect.addEventListener("change", performUploadsSearch)
    }
}

// Document Card Functionality
function initializeDocumentCards() {
    const documentCards = document.querySelectorAll(".document-type-card")

    documentCards.forEach((card) => {
        card.addEventListener("click", function () {
            const documentType = this.getAttribute("data-type")
            openUploadModal(documentType)
        })

        // Add keyboard support
        card.addEventListener("keydown", function (e) {
            if (e.key === "Enter" || e.key === " ") {
                e.preventDefault()
                const documentType = this.getAttribute("data-type")
                openUploadModal(documentType)
            }
        })
    })
}

// Document Type Search Functionality
function initDocumentTypeSearch() {
    const searchInput = document.getElementById("documentTypeSearch")
    if (!searchInput) return

    searchInput.addEventListener("input", function () {
        const searchTerm = this.value.toLowerCase().trim()
        const documentCards = document.querySelectorAll(".document-type-card")
        const miscCard = document.querySelector(".document-type-card[data-fallback='true']")
        let visibleCount = 0

        documentCards.forEach((card) => {
            const title = card.querySelector("h5")?.textContent.toLowerCase() || ""
            const description = card.querySelector("p")?.textContent.toLowerCase() || ""

            if (!searchTerm || title.includes(searchTerm) || description.includes(searchTerm)) {
                card.style.display = ""
                visibleCount++
            } else {
                card.style.display = "none"
            }
        })

        // If no matches found, show only Miscellaneous
        if (visibleCount === 0 && miscCard) {
            miscCard.style.display = ""
        }
    })
}

// Modal Functionality
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
}

// Drag and Drop Functionality
function initializeDragAndDrop() {
    const uploadAreas = document.querySelectorAll(".upload-area")

    uploadAreas.forEach((area) => {
        area.addEventListener("dragover", handleDragOver)
        area.addEventListener("dragleave", handleDragLeave)
        area.addEventListener("drop", handleDrop)
    })
}

// Event Handlers
function handleModalFileSelect(event) {
    const files = event.target.files
    displaySelectedFiles(files, "fileList")
}

function handleDragOver(event) {
    event.preventDefault()
    event.currentTarget.classList.add("dragover")
}

function handleDragLeave(event) {
    event.preventDefault()
    event.currentTarget.classList.remove("dragover")
}

function handleDrop(event) {
    event.preventDefault()
    event.currentTarget.classList.remove("dragover")

    const files = event.dataTransfer.files
    const uploadArea = event.currentTarget

    if (uploadArea.id === "modalUploadArea") {
        document.getElementById("documentFileInput").files = files
        handleModalFileSelect({ target: { files } })
    }
}

// Upload Functions
function openUploadModal(documentType) {
    console.log("Opening modal for:", documentType) // Debug log

    document.getElementById("selectedDocumentType").value = documentType
    document.getElementById("documentTypeDisplay").value = documentType

    // Show/hide custom document type field for Miscellaneous
    const customContainer = document.getElementById("customDocumentTypeContainer")
    if (documentType === "Miscellaneous") {
        customContainer.style.display = "block"
        document.getElementById("customDocumentType").required = true
    } else {
        customContainer.style.display = "none"
        document.getElementById("customDocumentType").required = false
    }

    // Clear previous values
    document.getElementById("documentFileName").value = ""
    document.getElementById("customDocumentType").value = ""

    // Clear file input and list
    document.getElementById("documentFileInput").value = ""
    document.getElementById("fileList").innerHTML = ""

    // Initialize and show modal
    const modalElement = document.getElementById("uploadModal")
    if (modalElement) {
        const modal = window.bootstrap.Modal.getOrCreateInstance(modalElement)
        modal.show()
    } else {
        console.error("Modal element not found")
    }
}

function uploadSelectedFiles() {
    const documentType = document.getElementById("selectedDocumentType").value
    const fileName = document.getElementById("documentFileName").value
    const fileInput = document.getElementById("documentFileInput")

    let finalDocumentType = documentType

    // Handle miscellaneous documents
    if (documentType === "Miscellaneous") {
        const customType = document.getElementById("customDocumentType").value
        if (!customType) {
            showAlert("Please enter a document type for miscellaneous documents.", "warning")
            return
        }
        finalDocumentType = customType
    }

    if (!fileInput.files.length) {
        showAlert("Please select at least one file.", "warning")
        return
    }

    uploadFiles(fileInput.files, finalDocumentType, fileName)
}

async function uploadFiles(files, documentType, customFileName) {
    if (!files || files.length === 0) {
        showAlert("No files selected", "error")
        return
    }

    showLoadingState(true)

    // Get antiforgery token
    const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value ||
                  document.querySelector('meta[name="request-verification-token"]')?.content || ''

    let successCount = 0
    let errorMessages = []

    // Upload each file individually (controller expects single file)
    for (const file of files) {
        // Reject oversized files here so the user isn't left waiting on an upload
        // the server will refuse. The server enforces the same limit regardless.
        if (file.size > MAX_UPLOAD_BYTES) {
            const sizeMb = (file.size / (1024 * 1024)).toFixed(1)
            errorMessages.push(`${file.name} is ${sizeMb} MB. The maximum size is ${MAX_UPLOAD_MB} MB.`)
            continue
        }

        const formData = new FormData()
        formData.append("file", file)
        formData.append("category", documentType)

        try {
            const response = await fetch('/Documents/Upload', {
                method: 'POST',
                body: formData,
                headers: {
                    'RequestVerificationToken': token
                }
            })

            if (response.status === 413) {
                errorMessages.push(`${file.name} is too large. The maximum size is ${MAX_UPLOAD_MB} MB.`)
                continue
            }

            const data = await response.json()

            if (response.ok && data.success) {
                successCount++
            } else {
                errorMessages.push(data.message || `Failed to upload ${file.name}`)
            }
        } catch (error) {
            errorMessages.push(`Error uploading ${file.name}: ${error.message}`)
        }
    }

    showLoadingState(false)

    if (successCount > 0) {
        showAlert(`Successfully uploaded ${successCount} file(s) for ${documentType}`, "success")
        updateUploadCount(documentType, successCount)

        // Refresh the uploads list
        if (typeof refreshUploadsList === 'function') {
            refreshUploadsList()
        } else {
            // Reload page to show new uploads
            setTimeout(() => window.location.reload(), 1500)
        }
    }

    if (errorMessages.length > 0) {
        showAlert(errorMessages.join(". "), "error")
    }

    clearFileInputs()

    // Close modal if open
    const modal = window.bootstrap.Modal.getInstance(document.getElementById("uploadModal"))
    if (modal) {
        modal.hide()
    }
}

// Uploads Search Functions
function performUploadsSearch() {
    const searchTerm = document.getElementById("uploadsSearch").value.toLowerCase()
    const filterType = document.getElementById("uploadsFilter").value.toLowerCase()

    const uploadItems = document.querySelectorAll(".upload-item")
    let visibleCount = 0

    uploadItems.forEach((item) => {
        const filename = item.getAttribute("data-filename") || ""
        const type = item.getAttribute("data-type") || ""

        const matchesSearch = !searchTerm || filename.includes(searchTerm)
        const matchesFilter = !filterType || type.includes(filterType)

        if (matchesSearch && matchesFilter) {
            item.style.display = "flex"
            visibleCount++
        } else {
            item.style.display = "none"
        }
    })

    // Show/hide search empty state
    const searchEmptyState = document.getElementById("searchEmptyState")
    const hasUploads = uploadItems.length > 0

    if (searchEmptyState) {
        if (visibleCount === 0 && hasUploads && (searchTerm || filterType)) {
            searchEmptyState.style.display = "block"
        } else {
            searchEmptyState.style.display = "none"
        }
    }

    // Optional: Show search results count
    if (searchTerm || filterType) {
        console.log(`Found ${visibleCount} uploads matching criteria`)
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
        const modal = window.bootstrap.Modal.getInstance(document.getElementById("profileModal"))
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
        const modal = window.bootstrap.Modal.getInstance(document.getElementById("passwordModal"))
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
        const modal = window.bootstrap.Modal.getInstance(document.getElementById("notificationModal"))
        modal.hide()
    }, 1000)
}

// Utility Functions
function displaySelectedFiles(files, containerId) {
    const container = document.getElementById(containerId)
    if (!container) return

    container.innerHTML = ""

    Array.from(files).forEach((file, index) => {
        const fileItem = createFileItem(file, index)
        container.appendChild(fileItem)
    })
}

function createFileItem(file, index) {
    const fileItem = document.createElement("div")
    fileItem.className = "file-item"
    fileItem.innerHTML = `
        <div class="file-info">
            <div class="file-icon">
                <i class="fas fa-file-${getFileIcon(file.type)}"></i>
            </div>
            <div class="file-details">
                <h6>${escapeHtml(file.name)}</h6>
                <p>${formatFileSize(file.size)} • ${escapeHtml(file.type || "Unknown type")}</p>
            </div>
        </div>
        <div class="file-actions">
            <button type="button" class="btn btn-sm btn-outline-danger" onclick="removeFile(${index})">
                <i class="fas fa-times"></i>
            </button>
        </div>
    `
    return fileItem
}

function getFileIcon(fileType) {
    if (fileType.includes("pdf")) return "pdf"
    if (fileType.includes("image")) return "image"
    if (fileType.includes("word") || fileType.includes("document")) return "word"
    return "alt"
}

function formatFileSize(bytes) {
    if (bytes === 0) return "0 Bytes"
    const k = 1024
    const sizes = ["Bytes", "KB", "MB", "GB"]
    const i = Math.floor(Math.log(bytes) / Math.log(k))
    return Number.parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + " " + sizes[i]
}

function updateUploadCount(documentType, count) {
    const documentCard = document.querySelector(`[data-type="${documentType}"]`)
    if (documentCard) {
        const countElement = documentCard.querySelector(".upload-count")
        const currentCount = Number.parseInt(countElement.textContent) || 0
        countElement.textContent = `${currentCount + count} files`
    }
}

function clearFileInputs() {
    const fileInputs = document.querySelectorAll('input[type="file"]')
    fileInputs.forEach((input) => {
        input.value = ""
    })

    const fileLists = document.querySelectorAll(".file-list")
    fileLists.forEach((list) => {
        list.innerHTML = ""
    })

    // Clear custom document type
    document.getElementById("customDocumentType").value = ""
    document.getElementById("documentFileName").value = ""
}

function showLoadingState(show) {
    const uploadBtn = document.getElementById("uploadDocumentBtn")
    if (uploadBtn) {
        if (show) {
            uploadBtn.innerHTML = '<span class="spinner me-2"></span>Uploading...'
            uploadBtn.disabled = true
        } else {
            uploadBtn.innerHTML = '<i class="fas fa-upload me-2"></i>Upload Documents'
            uploadBtn.disabled = false
        }
    }
}

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

function removeFile(index) {
    // Implementation for removing files from the list
    console.log("Removing file at index:", index)
}

// Document deletion
async function deleteDocument(documentId, fileName) {
    if (!confirm(`Are you sure you want to delete "${fileName}"? This action cannot be undone.`)) {
        return
    }

    // Get antiforgery token
    const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value ||
                  document.querySelector('meta[name="request-verification-token"]')?.content || ''

    try {
        const response = await fetch(`/Documents/Delete/${documentId}`, {
            method: 'DELETE',
            headers: {
                'RequestVerificationToken': token
            }
        })

        const data = await response.json()

        if (response.ok && data.success) {
            showAlert('Document deleted successfully', 'success')
            // Reload page to refresh counts and list
            setTimeout(() => window.location.reload(), 1000)
        } else {
            showAlert(data.message || 'Failed to delete document', 'error')
        }
    } catch (error) {
        showAlert('Error deleting document: ' + error.message, 'error')
    }
}

// ==========================================
// Quick Actions Functions
// ==========================================

function openQuickUpload() {
    // Open upload modal with default category
    const uploadModal = document.getElementById("uploadModal")
    if (uploadModal) {
        // Reset form and set default category
        const form = uploadModal.querySelector("form")
        if (form) form.reset()
        const categorySelect = document.getElementById("uploadCategory")
        if (categorySelect) categorySelect.value = "Miscellaneous"
        const modal = new bootstrap.Modal(uploadModal)
        modal.show()
    }
}

function openContactModal() {
    const contactModal = document.getElementById("contactModal")
    if (contactModal) {
        // Reset form
        document.getElementById("contactForm").reset()
        const modal = new bootstrap.Modal(contactModal)
        modal.show()
    }
}

function openAppointmentModal() {
    const appointmentModal = document.getElementById("appointmentModal")
    if (appointmentModal) {
        // Reset form
        document.getElementById("appointmentForm").reset()
        const modal = new bootstrap.Modal(appointmentModal)
        modal.show()
    }
}

function scrollToPending() {
    // Set filter to pending and scroll to CPA documents section
    const filter = document.getElementById("cpaDocsStatusFilter")
    if (filter) {
        filter.value = "pending"
        loadReceivedDocuments("pending")
    }
    const section = document.querySelector(".cpa-documents-section")
    if (section) {
        section.scrollIntoView({ behavior: "smooth", block: "start" })
    }
}

// Contact Form Submission
function initializeContactForm() {
    const contactForm = document.getElementById("contactForm")
    if (contactForm) {
        contactForm.addEventListener("submit", async (e) => {
            e.preventDefault()

            const submitBtn = document.getElementById("sendMessageBtn")
            const originalText = submitBtn.innerHTML
            submitBtn.disabled = true
            submitBtn.innerHTML = '<i class="fas fa-spinner fa-spin me-1"></i>Sending...'

            const formData = new FormData(contactForm)

            try {
                const response = await fetch("/ClientPortal/SendMessage", {
                    method: "POST",
                    body: formData,
                    headers: {
                        "RequestVerificationToken": document.querySelector('input[name="__RequestVerificationToken"]').value
                    }
                })

                const result = await response.json()

                bootstrap.Modal.getInstance(document.getElementById("contactModal")).hide()

                if (result.success) {
                    showAlert(result.message, "success")
                } else {
                    showAlert(result.message || "Failed to send message.", "error")
                }
            } catch (error) {
                showAlert("An error occurred. Please try again.", "error")
            } finally {
                submitBtn.disabled = false
                submitBtn.innerHTML = originalText
            }
        })
    }
}

// Appointment Form Submission
function initializeAppointmentForm() {
    const appointmentForm = document.getElementById("appointmentForm")
    if (appointmentForm) {
        appointmentForm.addEventListener("submit", async (e) => {
            e.preventDefault()

            const submitBtn = document.getElementById("requestAppointmentBtn")
            const originalText = submitBtn.innerHTML
            submitBtn.disabled = true
            submitBtn.innerHTML = '<i class="fas fa-spinner fa-spin me-1"></i>Sending...'

            const formData = new FormData(appointmentForm)

            try {
                const response = await fetch("/ClientPortal/RequestAppointment", {
                    method: "POST",
                    body: formData,
                    headers: {
                        "RequestVerificationToken": document.querySelector('input[name="__RequestVerificationToken"]').value
                    }
                })

                const result = await response.json()

                bootstrap.Modal.getInstance(document.getElementById("appointmentModal")).hide()

                if (result.success) {
                    showAlert(result.message, "success")
                } else {
                    showAlert(result.message || "Failed to send request.", "error")
                }
            } catch (error) {
                showAlert("An error occurred. Please try again.", "error")
            } finally {
                submitBtn.disabled = false
                submitBtn.innerHTML = originalText
            }
        })
    }
}

// ==========================================
// Documents from Your CPA Section
// ==========================================

// Category display order and configuration
const cpaDocumentCategories = {
    "Requires Signature": {
        icon: "fa-pen-nib",
        badgeClass: "bg-warning text-dark",
        highlight: true,
        displayName: "Requires Your Signature"
    },
    "Tax Return - For Review": {
        icon: "fa-file-alt",
        badgeClass: "bg-info",
        highlight: false,
        displayName: "Tax Returns for Review"
    },
    "Tax Return - Finalized": {
        icon: "fa-check-circle",
        badgeClass: "bg-success",
        highlight: false,
        displayName: "Finalized Tax Returns"
    }
}

// Global variable to store workflows
let clientWorkflows = []

// Get empty state message based on filter status
function getEmptyStateMessage(status) {
    switch(status) {
        case "pending":
            return "No documents require your attention"
        case "responded":
            return "No documents awaiting CPA review"
        case "resolved":
            return "No resolved documents"
        default:
            return "No documents from your CPA"
    }
}

// Update pending count badge
function updatePendingCountBadge(count) {
    const badge = document.getElementById("pendingCountBadge")
    if (badge) {
        if (count > 0) {
            badge.textContent = count
            badge.style.display = "inline-block"
        } else {
            badge.style.display = "none"
        }
    }
}

// Load received documents from the server
async function loadReceivedDocuments(status = "pending") {
    const loadingEl = document.getElementById("cpaDocsLoading")
    const emptyEl = document.getElementById("cpaDocsEmpty")
    const listEl = document.getElementById("cpaDocsList")

    // Skip if elements don't exist (not on dashboard page)
    if (!loadingEl || !emptyEl || !listEl) return

    try {
        // Build workflows URL with status filter
        const workflowsUrl = status ? `/ClientPortal/GetWorkflows?status=${status}` : "/ClientPortal/GetWorkflows"

        // Fetch both documents and workflows in parallel
        const [docsResponse, workflowsResponse, pendingCountResponse] = await Promise.all([
            fetch("/ClientPortal/ReceivedDocuments"),
            fetch(workflowsUrl),
            fetch("/ClientPortal/GetWorkflows?status=pending") // Always get pending count for badge
        ])

        const docsData = await docsResponse.json()
        const workflowsData = await workflowsResponse.json()
        const pendingCountData = await pendingCountResponse.json()

        // Store workflows globally for later use
        clientWorkflows = workflowsData.success ? workflowsData.workflows : []

        // Update pending count badge
        const pendingCount = pendingCountData.success ? pendingCountData.workflows.length : 0
        updatePendingCountBadge(pendingCount)

        // Hide loading
        loadingEl.style.display = "none"

        if (!docsData.success || !docsData.documents || docsData.documents.length === 0) {
            emptyEl.innerHTML = `
                <i class="fas fa-folder-open fa-2x mb-2 text-muted"></i>
                <p class="mb-0">${getEmptyStateMessage(status)}</p>
            `
            emptyEl.style.display = "block"
            listEl.innerHTML = ""
            return
        }

        // Merge workflow info into documents
        const documentsWithWorkflow = docsData.documents.map(doc => {
            const workflow = clientWorkflows.find(w => w.documentId === doc.id)
            return {
                ...doc,
                workflow: workflow || null
            }
        })

        // Filter documents based on status - only show documents that have matching workflows
        let filteredDocuments = documentsWithWorkflow
        if (status) {
            filteredDocuments = documentsWithWorkflow.filter(doc => doc.workflow !== null)
        }

        // Check if we have documents to display after filtering
        if (filteredDocuments.length === 0) {
            emptyEl.innerHTML = `
                <i class="fas fa-folder-open fa-2x mb-2 text-muted"></i>
                <p class="mb-0">${getEmptyStateMessage(status)}</p>
            `
            emptyEl.style.display = "block"
            listEl.innerHTML = ""
            return
        }

        // Hide empty state and render the documents with workflow info
        emptyEl.style.display = "none"
        renderReceivedDocuments(filteredDocuments, listEl)
    } catch (error) {
        console.error("Error loading received documents:", error)
        loadingEl.style.display = "none"
        emptyEl.innerHTML = `
            <i class="fas fa-exclamation-triangle fa-2x mb-2 text-warning"></i>
            <p class="mb-0">Unable to load documents.</p>
            <small>Please refresh the page to try again.</small>
        `
        emptyEl.style.display = "block"
    }
}

// Render received documents grouped by category
function renderReceivedDocuments(documents, container) {
    // Group documents by category
    const groupedDocs = {}

    documents.forEach(doc => {
        const category = doc.category || "Other"
        if (!groupedDocs[category]) {
            groupedDocs[category] = []
        }
        groupedDocs[category].push(doc)
    })

    // Define category order (Requires Signature first, then others)
    const categoryOrder = ["Requires Signature", "Tax Return - For Review", "Tax Return - Finalized"]

    // Get all categories and sort them
    const allCategories = Object.keys(groupedDocs)
    const sortedCategories = [
        ...categoryOrder.filter(cat => allCategories.includes(cat)),
        ...allCategories.filter(cat => !categoryOrder.includes(cat))
    ]

    let html = '<div class="cpa-docs-grid">'

    sortedCategories.forEach(category => {
        const docs = groupedDocs[category]
        const config = cpaDocumentCategories[category] || {
            icon: "fa-file",
            badgeClass: "bg-secondary",
            highlight: false,
            displayName: category
        }

        const highlightClass = config.highlight ? "cpa-category-highlight" : ""

        html += `
            <div class="cpa-category-group ${highlightClass}">
                <div class="cpa-category-header">
                    <span class="badge ${config.badgeClass} me-2">
                        <i class="fas ${config.icon} me-1"></i>
                        ${config.displayName}
                    </span>
                    <span class="text-muted small">${docs.length} document${docs.length !== 1 ? 's' : ''}</span>
                </div>
                <div class="cpa-docs-list">
        `

        docs.forEach(doc => {
            const sentDate = new Date(doc.sentDate).toLocaleDateString('en-US', {
                month: 'short',
                day: 'numeric',
                year: 'numeric'
            })

            // Check for workflow info
            const workflow = doc.workflow
            const hasAdminNotes = workflow && workflow.adminNotes
            const canRespond = workflow && workflow.canRespond
            const hasResponded = workflow && workflow.status === "Responded"

            // Build admin notes display
            let adminNotesHtml = ''
            if (hasAdminNotes) {
                adminNotesHtml = `
                    <div class="admin-notes-inline mt-1">
                        <small class="text-primary"><i class="fas fa-comment-alt me-1"></i>${escapeHtml(workflow.adminNotes)}</small>
                    </div>
                `
            }

            // Build status badge
            let statusBadgeHtml = ''
            if (hasResponded) {
                statusBadgeHtml = `<span class="badge bg-success workflow-status-badge ms-2"><i class="fas fa-check me-1"></i>Responded</span>`
            } else if (workflow && workflow.status === "Pending") {
                statusBadgeHtml = `<span class="badge bg-warning text-dark workflow-status-badge ms-2">Pending</span>`
            }

            // Build action buttons
            let actionsHtml = `<a href="/Documents/Download/${doc.id}" class="btn btn-sm btn-outline-primary" title="Download"><i class="fas fa-download"></i></a>`

            // Add View button if workflow exists
            if (workflow) {
                actionsHtml = `<button class="btn btn-sm btn-outline-info me-1" onclick="viewWorkflowDetails('${workflow.id}')" title="View Details"><i class="fas fa-eye"></i></button>` + actionsHtml
            }

            if (canRespond) {
                const escapedFileName = escapeHtml(doc.fileName).replace(/'/g, "\\'")
                const escapedNotes = hasAdminNotes ? escapeHtml(workflow.adminNotes).replace(/'/g, "\\'") : ''
                actionsHtml = `
                    <button type="button" class="btn btn-sm btn-primary me-1" onclick="openResponseModal('${workflow.id}', '${escapedFileName}', '${escapedNotes}')" title="Respond">
                        <i class="fas fa-reply me-1"></i>Respond
                    </button>
                    <button class="btn btn-sm btn-outline-info me-1" onclick="viewWorkflowDetails('${workflow.id}')" title="View Details"><i class="fas fa-eye"></i></button>
                    <a href="/Documents/Download/${doc.id}" class="btn btn-sm btn-outline-primary" title="Download"><i class="fas fa-download"></i></a>
                `
            }

            html += `
                <div class="cpa-doc-item">
                    <div class="cpa-doc-info">
                        <div class="cpa-doc-icon">
                            <i class="fas fa-file-pdf"></i>
                        </div>
                        <div class="cpa-doc-details">
                            <h6 class="mb-0">${escapeHtml(doc.fileName)}${statusBadgeHtml}</h6>
                            <small class="text-muted">Sent ${sentDate}</small>
                            ${adminNotesHtml}
                        </div>
                    </div>
                    <div class="cpa-doc-actions">
                        ${actionsHtml}
                    </div>
                </div>
            `
        })

        html += `
                </div>
            </div>
        `
    })

    html += '</div>'
    container.innerHTML = html
}

// Helper function to escape HTML to prevent XSS
function escapeHtml(text) {
    if (text === null || text === undefined) return ''
    const div = document.createElement('div')
    div.textContent = text
    return div.innerHTML
}

// ==========================================
// Workflow Details Modal Functions
// ==========================================

// Get badge class based on workflow status
function getStatusBadgeClass(status) {
    const statusLower = (status || "").toLowerCase()
    switch (statusLower) {
        case "pending":
            return "bg-warning text-dark"
        case "responded":
            return "bg-info"
        case "resolved":
            return "bg-success"
        default:
            return "bg-secondary"
    }
}

// Build timeline HTML for workflow
function buildWorkflowTimeline(workflow) {
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

    return timelineHtml || '<p class="text-muted">No timeline data available.</p>'
}

// View workflow details in modal
function viewWorkflowDetails(workflowId) {
    // Find the workflow in cached data
    const workflow = clientWorkflows.find(w => w.id === workflowId)
    if (!workflow) {
        showAlert("Workflow not found.", "error")
        return
    }

    // Populate status badge
    const statusBadge = document.getElementById("workflowStatusBadge")
    if (statusBadge) {
        statusBadge.textContent = workflow.status
        statusBadge.className = "badge ms-2 " + getStatusBadgeClass(workflow.status)
    }

    // Build timeline
    const timeline = document.getElementById("workflowTimeline")
    if (timeline) {
        timeline.innerHTML = buildWorkflowTimeline(workflow)
    }

    // Populate document info
    const docNameEl = document.getElementById("workflowDocumentName")
    if (docNameEl) {
        docNameEl.textContent = workflow.documentName || "Unknown"
    }

    const categoryEl = document.getElementById("workflowCategory")
    if (categoryEl) {
        categoryEl.textContent = workflow.category || "Unknown"
    }

    // Admin notes
    const adminNotesSection = document.getElementById("adminNotesSection")
    const adminNotesText = document.getElementById("workflowAdminNotes")
    if (adminNotesSection && adminNotesText) {
        if (workflow.adminNotes) {
            adminNotesSection.style.display = "block"
            adminNotesText.textContent = workflow.adminNotes
        } else {
            adminNotesSection.style.display = "none"
        }
    }

    // Download original document button
    const downloadOriginalBtn = document.getElementById("downloadOriginalBtn")
    if (downloadOriginalBtn) {
        if (workflow.documentId) {
            downloadOriginalBtn.onclick = () => {
                window.location.href = `/Documents/Download/${workflow.documentId}`
            }
            downloadOriginalBtn.style.display = "inline-block"
        } else {
            downloadOriginalBtn.style.display = "none"
        }
    }

    // Client response section
    const status = (workflow.status || "").toLowerCase()
    const clientResponseSection = document.getElementById("clientResponseSection")
    const responseTextSection = document.getElementById("responseTextSection")
    const workflowResponseText = document.getElementById("workflowResponseText")
    const downloadResponseBtn = document.getElementById("downloadResponseBtn")

    if (clientResponseSection) {
        if (status === "responded" || status === "resolved") {
            clientResponseSection.style.display = "block"

            // Response text
            if (responseTextSection && workflowResponseText) {
                if (workflow.clientResponseText) {
                    responseTextSection.style.display = "block"
                    workflowResponseText.textContent = workflow.clientResponseText
                } else {
                    responseTextSection.style.display = "none"
                }
            }

            // Response document download
            if (downloadResponseBtn) {
                if (workflow.clientResponseDocumentId) {
                    downloadResponseBtn.onclick = () => {
                        window.location.href = `/Documents/Download/${workflow.clientResponseDocumentId}`
                    }
                    downloadResponseBtn.style.display = "inline-block"
                } else {
                    downloadResponseBtn.style.display = "none"
                }
            }
        } else {
            clientResponseSection.style.display = "none"
        }
    }

    // Respond button (only for Pending)
    const respondBtn = document.getElementById("respondToWorkflowBtn")
    if (respondBtn) {
        if (workflow.status === "Pending" && workflow.canRespond) {
            respondBtn.style.display = "inline-block"
            respondBtn.onclick = () => {
                // Close this modal and open response modal
                const detailsModal = bootstrap.Modal.getInstance(document.getElementById("workflowDetailsModal"))
                if (detailsModal) {
                    detailsModal.hide()
                }
                openResponseModal(workflow.id, workflow.documentName, workflow.adminNotes || '')
            }
        } else {
            respondBtn.style.display = "none"
        }
    }

    // Show modal
    const modalElement = document.getElementById("workflowDetailsModal")
    if (modalElement) {
        const modal = new bootstrap.Modal(modalElement)
        modal.show()
    }
}

// ==========================================
// Workflow Response Functions
// ==========================================

// Open the response modal for a workflow
function openResponseModal(workflowId, documentName, adminNotes) {
    // Set the workflow ID
    document.getElementById("responseWorkflowId").value = workflowId

    // Set the document name
    document.getElementById("responseModalDocName").textContent = documentName

    // Show/hide admin notes
    const notesContainer = document.getElementById("responseModalNotes")
    const notesContent = document.getElementById("responseModalNotesContent")

    if (adminNotes && adminNotes.trim()) {
        notesContent.textContent = adminNotes
        notesContainer.style.display = "block"
    } else {
        notesContainer.style.display = "none"
    }

    // Clear form fields
    document.getElementById("responseText").value = ""
    document.getElementById("responseFile").value = ""

    // Show the modal
    const modalElement = document.getElementById("responseModal")
    if (modalElement) {
        const modal = window.bootstrap.Modal.getOrCreateInstance(modalElement)
        modal.show()
    }
}

// Submit the workflow response
async function submitWorkflowResponse() {
    const workflowId = document.getElementById("responseWorkflowId").value
    const responseText = document.getElementById("responseText").value
    const responseFile = document.getElementById("responseFile").files[0]

    // Validate input
    if (!responseText.trim() && !responseFile) {
        showAlert("Please provide a response message or attach a file.", "warning")
        return
    }

    // Get antiforgery token
    const token = document.querySelector('#workflowResponseForm input[name="__RequestVerificationToken"]')?.value ||
                  document.querySelector('input[name="__RequestVerificationToken"]')?.value || ''

    // Create form data
    const formData = new FormData()
    formData.append("workflowId", workflowId)
    if (responseText.trim()) {
        formData.append("responseText", responseText)
    }
    if (responseFile) {
        formData.append("responseFile", responseFile)
    }

    // Show loading state
    const submitBtn = document.querySelector('#responseModal .btn-primary')
    const originalBtnHtml = submitBtn.innerHTML
    submitBtn.innerHTML = '<span class="spinner-border spinner-border-sm me-1"></span>Submitting...'
    submitBtn.disabled = true

    try {
        const response = await fetch('/ClientPortal/SubmitWorkflowResponse', {
            method: 'POST',
            body: formData,
            headers: {
                'RequestVerificationToken': token
            }
        })

        const data = await response.json()

        if (response.ok && data.success) {
            // Close the modal
            const modal = window.bootstrap.Modal.getInstance(document.getElementById("responseModal"))
            if (modal) {
                modal.hide()
            }

            showAlert(data.message || "Response submitted successfully!", "success")

            // Reload the documents to show updated status
            loadReceivedDocuments()
        } else {
            showAlert(data.message || "Failed to submit response.", "error")
        }
    } catch (error) {
        console.error("Error submitting response:", error)
        showAlert("Error submitting response: " + error.message, "error")
    } finally {
        // Restore button state
        submitBtn.innerHTML = originalBtnHtml
        submitBtn.disabled = false
    }
}

// Ensure Bootstrap is available
const bootstrap = window.bootstrap
if (typeof bootstrap === "undefined") {
    console.error("Bootstrap is not loaded")
} else {
    console.log("Bootstrap loaded successfully")
}
