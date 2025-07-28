// Client Portal JavaScript

document.addEventListener("DOMContentLoaded", () => {
    initializeClientPortal()
})

function initializeClientPortal() {
    initializeFileUploads()
    initializeUploadsSearch()
    initializeDocumentCards()
    initializeModals()
    initializeDragAndDrop()
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

function uploadFiles(files, documentType, customFileName) {
    const formData = new FormData()

    Array.from(files).forEach((file, index) => {
        formData.append(`files[${index}]`, file)
    })

    formData.append("documentType", documentType)
    formData.append("customFileName", customFileName)

    // Show loading state
    showLoadingState(true)

    // Simulate upload (replace with actual API call)
    setTimeout(() => {
        showLoadingState(false)
        showAlert(`Successfully uploaded ${files.length} file(s) for ${documentType}`, "success")
        updateUploadCount(documentType, files.length)
        clearFileInputs()

        // Close modal if open
        const modal = window.bootstrap.Modal.getInstance(document.getElementById("uploadModal"))
        if (modal) {
            modal.hide()
        }
    }, 2000)
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
                <h6>${file.name}</h6>
                <p>${formatFileSize(file.size)} • ${file.type || "Unknown type"}</p>
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

function removeFile(index) {
    // Implementation for removing files from the list
    console.log("Removing file at index:", index)
}

// Quick Action Functions
function scheduleAppointment() {
    showAlert("Redirecting to appointment scheduling...", "info")
    // This would redirect to a scheduling system
}

function contactCPA() {
    showAlert("Opening contact form...", "info")
    // This would open a contact modal or redirect to contact page
}

// Ensure Bootstrap is available
const bootstrap = window.bootstrap
if (typeof bootstrap === "undefined") {
    console.error("Bootstrap is not loaded")
} else {
    console.log("Bootstrap loaded successfully")
}
