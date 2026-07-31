import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { BrowserRouter, Routes, Route, Navigate, useNavigate } from "react-router-dom";
import { AuthProvider, useAuth } from "./auth/authProvider";
import { useMe } from "./api/vssClient";
import { Spinner } from "./ui";

import { Login } from "./features/auth/Login";
import { Signup } from "./features/auth/Signup";
import { CheckInbox } from "./features/auth/CheckInbox";
import { LinkRecord } from "./features/auth/LinkRecord";
import { LinkSuccess } from "./features/auth/LinkSuccess";
import { VendorConsole } from "./features/vendor/VendorConsole";
import { VendorProfile } from "./features/vendor/VendorProfile";
import { ChangeSubmitted } from "./features/vendor/ChangeSubmitted";
import { AdminConsole } from "./features/admin/AdminConsole";
import { AdminChangeRequests } from "./features/admin/AdminChangeRequests";
import { AdminChangeDetail } from "./features/admin/AdminChangeDetail";
import { AdminVendors } from "./features/admin/AdminVendors";
import { AdminLinkRequests } from "./features/admin/AdminLinkRequests";
import { AdminErp } from "./features/admin/AdminErp";
import { AdminDocumentTypes } from "./features/admin/AdminDocumentTypes";
import { AdminContactCodes } from "./features/admin/AdminContactCodes";
import { AdminNotificationTypes } from "./features/admin/AdminNotificationTypes";

const queryClient = new QueryClient({
  defaultOptions: { queries: { retry: 1, refetchOnWindowFocus: false } },
});

/** Sends the user to the right place based on auth + link state. */
function RootRedirect() {
  const { isAuthenticated, role } = useAuth();
  const { data: me, isLoading } = useMe();
  if (!isAuthenticated) return <Navigate to="/login" replace />;
  if (role === "admin") return <Navigate to="/admin" replace />;
  if (isLoading) return <Spinner label="Loading your portal…" />;
  return <Navigate to={me?.linkState === "Linked" ? "/console" : "/link"} replace />;
}

/** Gate for authenticated app routes. */
function RequireAuth({ children }: { children: React.ReactNode }) {
  const { isAuthenticated } = useAuth();
  return isAuthenticated ? <>{children}</> : <Navigate to="/login" replace />;
}

function Shell() {
  useNavigate(); // ensure router context is present for children
  return (
    <Routes>
      <Route path="/" element={<RootRedirect />} />
      <Route path="/login" element={<Login />} />
      <Route path="/signup" element={<Signup />} />
      <Route path="/check-inbox" element={<CheckInbox />} />
      <Route path="/link" element={<RequireAuth><LinkRecord /></RequireAuth>} />
      <Route path="/link/success" element={<RequireAuth><LinkSuccess /></RequireAuth>} />
      <Route path="/console" element={<RequireAuth><VendorConsole /></RequireAuth>} />
      <Route path="/profile" element={<Navigate to="/profile/company" replace />} />
      <Route path="/profile/:tab" element={<RequireAuth><VendorProfile /></RequireAuth>} />
      <Route path="/submitted" element={<RequireAuth><ChangeSubmitted /></RequireAuth>} />

      {/* Admin (City staff) */}
      <Route path="/admin" element={<RequireAuth><AdminConsole /></RequireAuth>} />
      <Route path="/admin/vendors" element={<RequireAuth><AdminVendors /></RequireAuth>} />
      <Route path="/admin/link-requests" element={<RequireAuth><AdminLinkRequests /></RequireAuth>} />
      <Route path="/admin/change-requests" element={<RequireAuth><AdminChangeRequests /></RequireAuth>} />
      <Route path="/admin/change-requests/:id" element={<RequireAuth><AdminChangeDetail /></RequireAuth>} />
      <Route path="/admin/erp" element={<RequireAuth><AdminErp /></RequireAuth>} />
      <Route path="/admin/document-types" element={<RequireAuth><AdminDocumentTypes /></RequireAuth>} />
      <Route path="/admin/contact-codes" element={<RequireAuth><AdminContactCodes /></RequireAuth>} />
      <Route path="/admin/notification-types" element={<RequireAuth><AdminNotificationTypes /></RequireAuth>} />

      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}

export default function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <AuthProvider>
          <Shell />
        </AuthProvider>
      </BrowserRouter>
    </QueryClientProvider>
  );
}
